using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace JellyGate
{
    // Real world volumes layered over the strategic board.  They are deliberately shallow in
    // depth so the portrait 2.5D camera keeps its readable tactical framing, while the keep,
    // gates and cliffs are no longer baked pixels pretending to be buildings.
    public static class Crownfront3DWorld
    {
        private static readonly Dictionary<string, Material> Materials = new();
        private static readonly Dictionary<string, Material> AuthoredMaterials = new();

        public static void Build(Transform parent)
        {
            var root = new GameObject("Crownfront 3D World").transform;
            root.SetParent(parent, false);
            CreateLighting(root);
            // Authored FBX landmarks replace the discarded primitive landmark pass.  Their
            // colliders map to the same hand-authored navigation footprints used by gameplay.
            PlaceAuthored(root, "KayKit2p5D/World/Citadel", "Central Citadel", new Vector3(0f, -.88f, -1.18f),
                Vector3.one * .53f, new Vector3(2.8f, 2.35f, .72f));
            PlaceAuthored(root, "KayKit2p5D/World/GateWall", "North West Gate", new Vector3(-4.56f, 6.10f, -1.06f),
                Vector3.one * .45f, new Vector3(.7f, .8f, .55f));
            PlaceAuthored(root, "KayKit2p5D/World/GateWall", "North East Gate", new Vector3(4.56f, 6.10f, -1.06f),
                Vector3.one * .45f, new Vector3(.7f, .8f, .55f));
            PlaceAuthored(root, "KayKit2p5D/World/GateWall", "South West Gate", new Vector3(-4.56f, -6.10f, -1.06f),
                Vector3.one * .45f, new Vector3(.7f, .8f, .55f));
            PlaceAuthored(root, "KayKit2p5D/World/GateWall", "South East Gate", new Vector3(4.56f, -6.10f, -1.06f),
                Vector3.one * .45f, new Vector3(.7f, .8f, .55f));
        }

        public static void BuildLighting(Transform parent)
        {
            var root = new GameObject("Crownfront 3D Character Lighting").transform;
            root.SetParent(parent, false);
            CreateLighting(root);
        }

        private static void PlaceAuthored(Transform parent, string resourcePath, string name, Vector3 position,
            Vector3 scale, Vector3 colliderSize)
        {
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                Debug.LogWarning("Missing 3D landmark: " + resourcePath);
                return;
            }
            var landmark = Object.Instantiate(prefab, parent);
            landmark.name = name;
            landmark.transform.localPosition = position;
            landmark.transform.localRotation = Quaternion.identity;
            landmark.transform.localScale = scale;
            var collider = landmark.AddComponent<BoxCollider>();
            collider.size = colliderSize;
            var textureKey = resourcePath.Contains("GateWall") ? "KayKit2p5D/World/WorldNeutralAtlas" : "KayKit2p5D/World/WorldBlueAtlas";
            var material = AuthoredMaterial(textureKey);
            foreach (var renderer in landmark.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.sharedMaterial = material;
            }
        }

        private static Material AuthoredMaterial(string textureKey)
        {
            if (AuthoredMaterials.TryGetValue(textureKey, out var material) && material != null) return material;
            var texture = Resources.Load<Texture2D>(textureKey);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            material = new Material(shader) { name = "Crownfront Landmark " + textureKey, renderQueue = (int)RenderQueue.Transparent + 12 };
            if (texture != null)
            {
                material.mainTexture = texture;
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            }
            AuthoredMaterials[textureKey] = material;
            return material;
        }

        private static void CreateLighting(Transform parent)
        {
            var key = new GameObject("Citadel Warm Key").AddComponent<Light>();
            key.transform.SetParent(parent, false);
            key.type = LightType.Directional;
            key.color = new Color(1f, .86f, .67f);
            key.intensity = 1.28f;
            key.transform.rotation = Quaternion.Euler(28f, -35f, 0f);

            var fill = new GameObject("Citadel Sky Fill").AddComponent<Light>();
            fill.transform.SetParent(parent, false);
            fill.type = LightType.Directional;
            fill.color = new Color(.38f, .58f, 1f);
            fill.intensity = .43f;
            fill.transform.rotation = Quaternion.Euler(-18f, 28f, 0f);
        }

        private static void CreateCitadel(Transform parent)
        {
            var root = new GameObject("Central Citadel 3D").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(0f, -.42f, .42f);
            var stone = Mat(new Color(.35f, .39f, .42f), .16f, .34f);
            var lightStone = Mat(new Color(.57f, .59f, .56f), .08f, .38f);
            var darkStone = Mat(new Color(.18f, .21f, .25f), .08f, .28f);
            var blue = Mat(new Color(.04f, .20f, .62f), .24f, .48f);
            var gold = Mat(new Color(1f, .63f, .13f), .8f, .7f);
            var wood = Mat(new Color(.15f, .075f, .035f), .03f, .25f);

            var foundation = Block(root, "Citadel Foundation", new Vector3(0f, .10f, .05f), new Vector3(3.28f, 1.34f, .38f), darkStone, true);
            foundation.gameObject.AddComponent<BoxCollider>().size = new Vector3(3.2f, 1.22f, .45f);
            Block(root, "Citadel Main Wall", new Vector3(0f, .36f, -.03f), new Vector3(2.48f, 1.07f, .34f), stone, false);
            BrickRow(root, new Vector3(0f, .30f, -.22f), 2.18f, .88f, lightStone);
            Block(root, "Citadel Gate Shadow", new Vector3(0f, -.08f, -.26f), new Vector3(.72f, .68f, .07f), wood, false);
            Arch(root, "Citadel Gold Arch", new Vector3(0f, .16f, -.30f), .46f, .44f, gold);

            for (var i = -1; i <= 1; i += 2)
            {
                var tower = Cylinder(root, "Citadel Tower", new Vector3(i * 1.15f, .62f, -.02f), new Vector3(.40f, .78f, .35f), lightStone, true);
                tower.gameObject.AddComponent<BoxCollider>().size = new Vector3(.76f, 1.48f, .42f);
                Cone(tower, "Blue Tower Roof", new Vector3(0f, .73f, 0f), .36f, .64f, blue);
                Cylinder(tower, "Tower Gold Finial", new Vector3(0f, 1.08f, -.01f), new Vector3(.055f, .13f, .055f), gold, false);
                Banner(tower, new Vector3(0f, .18f, -.30f), blue, gold);
            }
            for (var i = -2; i <= 2; i++)
                Block(root, "Citadel Battlement", new Vector3(i * .44f, 1.02f, -.18f), new Vector3(.20f, .20f, .16f), lightStone, false);
            Banner(root, new Vector3(0f, .78f, -.31f), blue, gold);
        }

        private static void CreateGatehouse(Transform parent, Vector2 position, float side, float vertical, string name)
        {
            var root = new GameObject(name + " 3D").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(position.x, position.y - .16f * vertical, .36f);
            root.localScale = Vector3.one * .6f;
            var stone = Mat(new Color(.44f, .46f, .43f), .1f, .32f);
            var blue = Mat(new Color(.05f, .23f, .68f), .25f, .5f);
            var gold = Mat(new Color(1f, .63f, .13f), .78f, .7f);
            var dark = Mat(new Color(.12f, .08f, .05f), .02f, .22f);
            Block(root, "Gate Wall", new Vector3(0f, .07f, 0f), new Vector3(1.24f, .72f, .26f), stone, true);
            Block(root, "Gate Opening", new Vector3(0f, -.14f, -.18f), new Vector3(.42f, .38f, .04f), dark, false);
            for (var i = -1; i <= 1; i += 2)
            {
                var tower = Cylinder(root, "Gate Tower", new Vector3(i * .52f, .34f, -.01f), new Vector3(.23f, .54f, .22f), stone, true);
                Cone(tower, "Gate Roof", new Vector3(0f, .48f, 0f), .22f, .38f, blue);
            }
            Banner(root, new Vector3(side * .19f, .27f, -.22f), blue, gold);
            root.gameObject.AddComponent<BoxCollider>().size = new Vector3(1.18f, .72f, .34f);
        }

        private static void CreateHighland(Transform parent, Vector2 position, Vector2 radius)
        {
            var root = new GameObject("Raised Highland 3D").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(position.x, position.y, .48f);
            var deepRock = Mat(new Color(.16f, .20f, .18f), .03f, .24f);
            var rock = Mat(new Color(.32f, .35f, .30f), .06f, .3f);
            var grass = Mat(new Color(.18f, .43f, .18f), .04f, .33f);
            var trunk = Mat(new Color(.20f, .10f, .035f), .02f, .25f);
            var leaves = Mat(new Color(.05f, .28f, .10f), .02f, .28f);

            var baseRock = Sphere(root, "Cliff Base", new Vector3(0f, -.02f, .02f), new Vector3(radius.x * 2.2f, radius.y * 2.05f, .35f), deepRock, true);
            baseRock.gameObject.AddComponent<BoxCollider>().size = new Vector3(radius.x * 1.8f, radius.y * 1.66f, .3f);
            Sphere(root, "Grass Crown", new Vector3(0f, .13f, -.18f), new Vector3(radius.x * 1.83f, radius.y * 1.68f, .16f), grass, false);
            for (var i = 0; i < 7; i++)
            {
                var angle = i * Mathf.PI * 2f / 7f + position.x * .43f;
                var distance = .34f + (i % 3) * .11f;
                var p = new Vector3(Mathf.Cos(angle) * radius.x * distance, Mathf.Sin(angle) * radius.y * distance, -.26f);
                Sphere(root, "Cliff Facet", p, new Vector3(.22f, .18f, .13f), rock, false);
            }
            for (var i = 0; i < 3; i++)
            {
                var a = i * 2.13f + position.y;
                var x = Mathf.Cos(a) * radius.x * .42f;
                var y = Mathf.Sin(a) * radius.y * .34f;
                var tree = Cylinder(root, "Highland Tree Trunk", new Vector3(x, y - .04f, -.31f), new Vector3(.055f, .19f, .05f), trunk, false);
                Cone(tree, "Highland Tree Crown", new Vector3(0f, .24f, -.02f), .22f, .48f, leaves);
            }
        }

        private static void BrickRow(Transform parent, Vector3 center, float width, float height, Material material)
        {
            const int columns = 6;
            const int rows = 3;
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var x = (column - (columns - 1) * .5f) * (width / columns) + (row % 2 == 0 ? 0f : .035f);
                var y = (row - 1) * (height / rows);
                Block(parent, "Cut Stone", center + new Vector3(x, y, 0f), new Vector3(width / columns - .018f, height / rows - .022f, .028f), material, false);
            }
        }

        private static void Arch(Transform parent, string name, Vector3 position, float radius, float height, Material material)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            for (var i = 0; i < 7; i++)
            {
                var angle = Mathf.Lerp(205f, -25f, i / 6f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius + height * .18f, 0f);
                var block = Block(root, "Arch Stone", p, new Vector3(.17f, .14f, .045f), material, false);
                block.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + 90f);
            }
        }

        private static void Banner(Transform parent, Vector3 position, Material fabric, Material trim)
        {
            var pole = Cylinder(parent, "Banner Pole", position + new Vector3(-.09f, .07f, .01f), new Vector3(.023f, .31f, .023f), trim, false);
            Block(pole, "Blue Banner", new Vector3(.09f, -.04f, -.025f), new Vector3(.17f, .25f, .018f), fabric, false);
        }

        private static Transform Block(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            return Shape(parent, name, PrimitiveType.Cube, position, scale, material, collider);
        }

        private static Transform Sphere(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            return Shape(parent, name, PrimitiveType.Sphere, position, scale, material, collider);
        }

        private static Transform Cylinder(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            return Shape(parent, name, PrimitiveType.Cylinder, position, scale, material, collider);
        }

        private static Transform Shape(Transform parent, string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            var node = GameObject.CreatePrimitive(primitive);
            node.name = name;
            node.transform.SetParent(parent, false);
            node.transform.localPosition = position;
            node.transform.localScale = scale;
            node.GetComponent<Renderer>().sharedMaterial = material;
            node.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
            node.GetComponent<Renderer>().receiveShadows = true;
            if (!collider)
            {
                var c = node.GetComponent<Collider>();
                if (c != null) Object.Destroy(c);
            }
            return node.transform;
        }

        private static Transform Cone(Transform parent, string name, Vector3 position, float radius, float height, Material material)
        {
            const int sides = 12;
            var vertices = new List<Vector3> { new(0f, height * .5f, 0f), new(0f, -height * .5f, 0f) };
            for (var i = 0; i < sides; i++)
            {
                var a = i / (float)sides * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(a) * radius, -height * .5f, Mathf.Sin(a) * radius));
            }
            var triangles = new List<int>();
            for (var i = 0; i < sides; i++)
            {
                var next = 2 + (i + 1) % sides;
                triangles.Add(0); triangles.Add(2 + i); triangles.Add(next);
                triangles.Add(1); triangles.Add(next); triangles.Add(2 + i);
            }
            var mesh = new Mesh { name = name + " Mesh" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            var node = new GameObject(name);
            node.transform.SetParent(parent, false);
            node.transform.localPosition = position;
            node.AddComponent<MeshFilter>().sharedMesh = mesh;
            node.AddComponent<MeshRenderer>().sharedMaterial = material;
            return node.transform;
        }

        private static Material Mat(Color color, float metallic, float smoothness)
        {
            var key = $"{ColorUtility.ToHtmlStringRGBA(color)}:{Mathf.RoundToInt(metallic * 20f)}:{Mathf.RoundToInt(smoothness * 20f)}";
            if (Materials.TryGetValue(key, out var material) && material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            material = new Material(shader) { name = "World " + key, enableInstancing = true };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            Materials[key] = material;
            return material;
        }
    }
}
