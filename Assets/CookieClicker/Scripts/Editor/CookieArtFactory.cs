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

        /// <summary>파티클용: 국물 거품 돔, 십자 모양 반짝이.</summary>
        public static Sprite SoupBubble => GetOrCreate("SoupBubble", 64, 64, SoupBubblePixel, Vector4.zero);
        public static Sprite Sparkle => GetOrCreate("Sparkle", 128, 128, SparklePixel, Vector4.zero);

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

        /// <summary>
        /// 국물이 부풀어 오른 빨간 거품 돔. 비누방울처럼 투명하면 재료 색에 묻히므로 국물 색을 칠하고,
        /// 가장자리는 어둡게·가운데 위쪽은 밝게 해서 볼록해 보이게 한 뒤 흰 반사광을 찍는다.
        /// </summary>
        private static Color SoupBubblePixel(int x, int y, int w, int h)
        {
            float r = Radius(x, y, w);
            if (r > 1f) return Color.clear;
            var uv = new Vector2((x + 0.5f) / w, (y + 0.5f) / h);

            float light = Mathf.Clamp01(1f - Vector2.Distance(uv, new Vector2(0.42f, 0.6f)) * 1.6f);
            var c = Color.Lerp(new Color(0.5f, 0.09f, 0.05f), new Color(0.98f, 0.4f, 0.2f), light);
            // 가장자리 얇은 밝은 테 (표면장력으로 빛나는 막)
            c = Color.Lerp(c, new Color(1f, 0.72f, 0.5f), Mathf.Clamp01((r - 0.78f) / 0.1f) * 0.85f);
            float highlight = Mathf.Clamp01(1f - Vector2.Distance(uv, new Vector2(0.36f, 0.68f)) * 6f);
            c = Color.Lerp(c, Color.white, highlight * highlight * 1.5f);
            // 가운데는 살짝 비쳐서 아래 국물이 보이고, 테두리와 반사광은 진하게 (방울토마토처럼 보이지 않게)
            float body = Mathf.Lerp(0.7f, 1f, r * r);
            c.a = Mathf.Clamp01((1f - r) * w * 0.3f) * Mathf.Max(body, highlight);
            return c;
        }

        private static Color SparklePixel(int x, int y, int w, int h)
        {
            var p = new Vector2((x + 0.5f) / w * 2f - 1f, (y + 0.5f) / h * 2f - 1f);
            // 가로·세로로 가늘고 긴 빛줄기 + 가운데 둥근 빛
            float cross = Mathf.Max(
                Mathf.Clamp01(1f - Mathf.Abs(p.y) * 14f) * Mathf.Clamp01(1f - Mathf.Abs(p.x)),
                Mathf.Clamp01(1f - Mathf.Abs(p.x) * 14f) * Mathf.Clamp01(1f - Mathf.Abs(p.y)));
            float core = Mathf.Clamp01(1f - p.magnitude * 3f);
            return new Color(1, 1, 1, Mathf.Clamp01(cross + core * core));
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

            EditorFolders.Ensure(path.Substring(0, path.LastIndexOf('/')));
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader);
            material.SetColor(material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
