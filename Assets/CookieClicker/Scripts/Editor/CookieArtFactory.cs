using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CookieClicker.EditorTools
{
    /// <summary>
    /// UI에 쓰는 단순한 스프라이트(원, 둥근 사각형, 후광, 2D 쿠키)를 PNG 파일로 만들어 둔다.
    /// 파일로 저장되므로 나중에 디자이너가 만든 이미지로 같은 경로에 덮어쓰기만 하면 교체된다.
    /// </summary>
    public static class CookieArtFactory
    {
        public const string ArtFolder = "Assets/CookieClicker/Art/Generated";

        public static Sprite RoundedRect => GetOrCreate("RoundedRect", 64, 64, RoundedRectPixel, new Vector4(22, 22, 22, 22));
        public static Sprite SoftCircle => GetOrCreate("SoftCircle", 256, 256, SoftCirclePixel, Vector4.zero);
        public static Sprite Rays => GetOrCreate("Rays", 512, 512, RaysPixel, Vector4.zero);
        public static Sprite Background => GetOrCreate("BackgroundGradient", 8, 256, GradientPixel, Vector4.zero);
        public static Sprite Cookie => GetOrCreate("Cookie2D", 128, 128,
            (x, y, w, h) => CookiePixel(x, y, w, new Color(0.83f, 0.6f, 0.33f), new Color(0.32f, 0.18f, 0.1f)), Vector4.zero);
        public static Sprite GoldenCookie => GetOrCreate("GoldenCookie2D", 128, 128,
            (x, y, w, h) => CookiePixel(x, y, w, new Color(1f, 0.82f, 0.3f), new Color(0.75f, 0.5f, 0.1f)), Vector4.zero);

        private static Sprite GetOrCreate(string name, int width, int height, Func<int, int, int, int, Color> pixel, Vector4 border)
        {
            string path = $"{ArtFolder}/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            EditorFolders.Ensure(ArtFolder);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = pixel(x, y, width, height);
            texture.SetPixels(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Color RoundedRectPixel(int x, int y, int w, int h)
        {
            const float radius = 18f;
            float cx = Mathf.Clamp(x + 0.5f, radius, w - radius);
            float cy = Mathf.Clamp(y + 0.5f, radius, h - radius);
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
            return new Color(1, 1, 1, Mathf.Clamp01(radius - d));
        }

        private static Color SoftCirclePixel(int x, int y, int w, int h)
        {
            float r = Radius(x, y, w);
            float a = Mathf.Clamp01(1f - r);
            return new Color(1, 1, 1, a * a);
        }

        private static Color RaysPixel(int x, int y, int w, int h)
        {
            float r = Radius(x, y, w);
            if (r >= 1f) return Color.clear;
            float angle = Mathf.Atan2(y - w * 0.5f, x - w * 0.5f);
            float ray = Mathf.Cos(angle * 12f) > 0.2f ? 1f : 0.15f;
            return new Color(1, 1, 1, ray * Mathf.Pow(1f - r, 1.4f));
        }

        private static Color GradientPixel(int x, int y, int w, int h)
        {
            return Color.Lerp(new Color(0.03f, 0.05f, 0.11f), new Color(0.12f, 0.2f, 0.36f), y / (float)(h - 1));
        }

        private static Color CookiePixel(int x, int y, int size, Color dough, Color chip)
        {
            float r = Radius(x, y, size);
            if (r > 1f) return Color.clear;

            Color c = Color.Lerp(dough, dough * 0.7f, Mathf.InverseLerp(0.75f, 1f, r));
            c.a = Mathf.Clamp01((1f - r) * size * 0.5f);

            // 고정 시드라서 매번 같은 위치에 초코칩이 찍힌다.
            var random = new System.Random(7);
            for (int i = 0; i < 9; i++)
            {
                var center = new Vector2((float)random.NextDouble() * 1.2f - 0.6f, (float)random.NextDouble() * 1.2f - 0.6f);
                float chipRadius = 0.08f + (float)random.NextDouble() * 0.06f;
                var p = new Vector2(x / (size * 0.5f) - 1f, y / (size * 0.5f) - 1f);
                if (Vector2.Distance(p, center) < chipRadius) c = new Color(chip.r, chip.g, chip.b, c.a);
            }
            return c;
        }

        private static float Radius(int x, int y, int size)
        {
            float half = size * 0.5f;
            return new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude / half;
        }

        /// <summary>3D 모델을 못 불러왔을 때 쓰는 대체 쿠키 재질.</summary>
        public static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"{ArtFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            EditorFolders.Ensure(ArtFolder);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader);
            material.SetColor(material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
