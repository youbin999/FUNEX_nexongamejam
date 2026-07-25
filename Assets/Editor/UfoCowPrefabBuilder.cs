using UnityEditor;
using UnityEngine;

public static class UfoCowPrefabBuilder
{
    public static void Build()
    {
        const string folder = "Assets/02_Sprite/12_UfoCow";
        ImportSprite(folder + "/ufo.png", 300f);
        ImportSprite(folder + "/cow.png", 300f);
        ImportSprite(folder + "/pasture_bg.png", 100f);

        GameObject root = new GameObject("UfoCowGame");
        SpriteRenderer background = Child(root.transform, "Background", folder + "/pasture_bg.png", 0);
        background.transform.localScale = new Vector3(1.0f, 1.0f, 1f);
        background.drawMode = SpriteDrawMode.Sliced;
        background.size = new Vector2(17.78f, 10f);

        SpriteRenderer ufo = Child(root.transform, "UFO", folder + "/ufo.png", 5);
        ufo.transform.localPosition = new Vector3(0f, 3.25f, 0f);
        ufo.transform.localScale = Vector3.one * 0.85f;

        GameObject beam = new GameObject("Beam");
        beam.transform.SetParent(ufo.transform, false);
        beam.transform.localPosition = new Vector3(0f, -2.1f, 0f);
        SpriteRenderer beamRenderer = beam.AddComponent<SpriteRenderer>();
        beamRenderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        beamRenderer.color = new Color(0.25f, 1f, 1f, 0.32f);
        beamRenderer.sortingOrder = 3;
        beam.transform.localScale = new Vector3(1.7f, 4f, 1f);
        beam.SetActive(false);

        GameObject herd = new GameObject("Cows");
        herd.transform.SetParent(root.transform, false);
        float[] xs = { -6.4f, -3.9f, -1.35f, 1.25f, 3.9f, 6.35f };
        for (int i = 0; i < xs.Length; i++)
        {
            SpriteRenderer cow = Child(herd.transform, "Cow_" + (i + 1), folder + "/cow.png", 4);
            cow.transform.localPosition = new Vector3(xs[i], -3.15f + (i % 2) * 0.18f, 0f);
            cow.transform.localScale = Vector3.one * (0.55f + (i % 3) * 0.04f);
            if (i % 2 == 1) cow.flipX = true;
        }

        UfoCowMiniGame game = root.AddComponent<UfoCowMiniGame>();
        SerializedObject serialized = new SerializedObject(game);
        serialized.FindProperty("timeLimit").floatValue = 8f;
        serialized.FindProperty("gameDescription").stringValue = "A/D로 이동! SPACE로 소를 전부 빨아들여!";
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, "Assets/03_Prefabs/UfoCowGame.prefab");
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static SpriteRenderer Child(Transform parent, string name, string spritePath, int order)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        renderer.sortingOrder = order;
        return renderer;
    }

    private static void ImportSprite(string path, float pixelsPerUnit)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }
}
