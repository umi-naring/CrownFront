param(
    [string]$Alias = 'crownfront-upload'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$workspacePath = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$buildScript = Join-Path $PSScriptRoot 'Build-CrownfrontPlayBundle.ps1'
$outputPath = Join-Path $workspacePath 'outputs\Crownfront-v1.00-code4.aab'
$statusPath = Join-Path $workspacePath 'outputs\Crownfront-v1.00-code4-signing-status.txt'

$form = New-Object System.Windows.Forms.Form
$form.Text = 'CROWNFRONT Google Play AAB Signing'
$form.StartPosition = 'CenterScreen'
$form.Size = New-Object System.Drawing.Size(520, 285)
$form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::FixedDialog
$form.MaximizeBox = $false
$form.MinimizeBox = $false
$form.TopMost = $true

$intro = New-Object System.Windows.Forms.Label
$intro.Location = New-Object System.Drawing.Point(22, 18)
$intro.Size = New-Object System.Drawing.Size(460, 42)
$intro.Text = "Enter the existing Google Play upload-key passwords.`r`nThe values are not written to project files or logs."
$form.Controls.Add($intro)

$storeLabel = New-Object System.Windows.Forms.Label
$storeLabel.Location = New-Object System.Drawing.Point(22, 72)
$storeLabel.Size = New-Object System.Drawing.Size(150, 22)
$storeLabel.Text = 'Keystore password'
$form.Controls.Add($storeLabel)

$storeBox = New-Object System.Windows.Forms.TextBox
$storeBox.Location = New-Object System.Drawing.Point(180, 69)
$storeBox.Size = New-Object System.Drawing.Size(292, 24)
$storeBox.UseSystemPasswordChar = $true
$form.Controls.Add($storeBox)

$sameBox = New-Object System.Windows.Forms.CheckBox
$sameBox.Location = New-Object System.Drawing.Point(180, 102)
$sameBox.Size = New-Object System.Drawing.Size(292, 24)
$sameBox.Text = 'Alias password is the same'
$sameBox.Checked = $true
$form.Controls.Add($sameBox)

$aliasLabel = New-Object System.Windows.Forms.Label
$aliasLabel.Location = New-Object System.Drawing.Point(22, 140)
$aliasLabel.Size = New-Object System.Drawing.Size(150, 22)
$aliasLabel.Text = 'Alias password'
$form.Controls.Add($aliasLabel)

$aliasBox = New-Object System.Windows.Forms.TextBox
$aliasBox.Location = New-Object System.Drawing.Point(180, 137)
$aliasBox.Size = New-Object System.Drawing.Size(292, 24)
$aliasBox.UseSystemPasswordChar = $true
$aliasBox.Enabled = $false
$form.Controls.Add($aliasBox)

$sameBox.Add_CheckedChanged({
    $aliasBox.Enabled = -not $sameBox.Checked
})

$buildButton = New-Object System.Windows.Forms.Button
$buildButton.Location = New-Object System.Drawing.Point(258, 190)
$buildButton.Size = New-Object System.Drawing.Size(102, 34)
$buildButton.Text = 'Build signed AAB'
$buildButton.DialogResult = [System.Windows.Forms.DialogResult]::OK
$form.AcceptButton = $buildButton
$form.Controls.Add($buildButton)

$cancelButton = New-Object System.Windows.Forms.Button
$cancelButton.Location = New-Object System.Drawing.Point(370, 190)
$cancelButton.Size = New-Object System.Drawing.Size(102, 34)
$cancelButton.Text = 'Cancel'
$cancelButton.DialogResult = [System.Windows.Forms.DialogResult]::Cancel
$form.CancelButton = $cancelButton
$form.Controls.Add($cancelButton)

$form.Add_Shown({ $storeBox.Focus() })
$result = $form.ShowDialog()
if ($result -ne [System.Windows.Forms.DialogResult]::OK) {
    Set-Content -LiteralPath $statusPath -Value 'CANCELLED' -Encoding UTF8
    exit 2
}

$keystorePassword = $storeBox.Text
$aliasPassword = if ($sameBox.Checked) { $storeBox.Text } else { $aliasBox.Text }
if ([string]::IsNullOrWhiteSpace($keystorePassword) -or [string]::IsNullOrWhiteSpace($aliasPassword)) {
    [System.Windows.Forms.MessageBox]::Show('Enter both passwords.', 'Input required') | Out-Null
    Set-Content -LiteralPath $statusPath -Value 'FAILED: missing password' -Encoding UTF8
    exit 3
}

try {
    Set-Content -LiteralPath $statusPath -Value 'BUILDING' -Encoding UTF8
    & $buildScript -KeystorePassword $keystorePassword -AliasPassword $aliasPassword -Alias $Alias -OutputPath $outputPath
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $outputPath)) {
        throw 'Signed AAB was not produced.'
    }
    Set-Content -LiteralPath $statusPath -Value 'SUCCESS' -Encoding UTF8
    [System.Windows.Forms.MessageBox]::Show("Signed AAB created:`r`n$outputPath", 'Complete') | Out-Null
}
catch {
    Set-Content -LiteralPath $statusPath -Value ("FAILED: " + $_.Exception.Message) -Encoding UTF8
    [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, 'Build failed') | Out-Null
    exit 1
}
finally {
    $keystorePassword = $null
    $aliasPassword = $null
    $storeBox.Text = ''
    $aliasBox.Text = ''
    $form.Dispose()
}
