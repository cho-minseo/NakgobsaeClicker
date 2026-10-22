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

        /// <summary>"동네 평상" 배경: 하늘, 겹친 언덕 3장, 구름, 평상(나무 탁자).</summary>
        public static Sprite Sky => GetOrCreate("Sky", 8, 512, SkyPixel, Vector4.zero);
        public static Sprite HillFar => GetOrCreate("Hill_Far", 1024, 256, (x, y, w, h) => HillPixel(x, y, w, h, 0.62f, 0.16f, 1.3f, 0.4f), Vector4.zero);
        public static Sprite HillMid => GetOrCreate("Hill_Mid", 1024, 256, (x, y, w, h) => HillPixel(x, y, w, h, 0.6f, 0.14f, 1.9f, 2.1f), Vector4.zero);
        public static Sprite HillNear => GetOrCreate("Hill_Near", 1024, 256, (x, y, w, h) => HillPixel(x, y, w, h, 0.66f, 0.1f, 1.6f, 4.2f), Vector4.zero);
        public static Sprite Cloud => GetOrCreate("Cloud", 256, 128, CloudPixel, Vector4.zero);
        public static Sprite Table => GetOrCreate("Table", 1024, 320, TablePixel, Vector4.zero);
        /// <summary>속보 띠의 빨간 이름표 (오른쪽 끝이 비스듬히 잘린 모양). 9-slice라 가로로 늘려도 비스듬함이 유지된다.</summary>
        public static Sprite SlantTag => GetOrCreate("SlantTag", 128, 64, SlantTagPixel, new Vector4(4, 0, 28, 0));
        public static Sprite Dot => GetOrCreate("Dot", 32, 32, (x, y, w, h) => new Color(1, 1, 1, Mathf.Clamp01((1f - Radius(x, y, w)) * w * 0.5f)), Vector4.zero);

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

        /// <summary>위는 맑은 하늘색, 지평선 쪽은 해 질 녘 살구색.</summary>
        private static Color SkyPixel(int x, int y, int w, int h)
        {
            float t = y / (float)(h - 1);
            var peach = new Color(0.97f, 0.82f, 0.66f);
            var sand = new Color(0.96f, 0.86f, 0.75f);
            var pale = new Color(0.725f, 0.863f, 0.937f);
            var blue = new Color(0.557f, 0.773f, 0.918f);
            if (t < 0.4f) return Color.Lerp(peach, sand, t / 0.4f);
            if (t < 0.62f) return Color.Lerp(sand, pale, (t - 0.4f) / 0.22f);
            return Color.Lerp(pale, blue, (t - 0.62f) / 0.38f);
        }

        /// <summary>완만한 언덕 실루엣 (흰색, Image 색으로 칠한다). height = 평균 높이, amplitude = 굴곡, phase로 모양을 바꾼다.</summary>
        private static Color HillPixel(int x, int y, int w, int h, float height, float amplitude, float waves, float phase)
        {
            float u = x / (float)w * Mathf.PI * 2f;
            float top = height + amplitude * (Mathf.Sin(u * waves * 0.5f + phase) * 0.7f + Mathf.Sin(u * waves * 1.3f + phase * 1.7f) * 0.3f);
            float edge = top * h - y;
            return new Color(1, 1, 1, Mathf.Clamp01(edge + 0.5f));
        }

        /// <summary>몽실한 구름: 바닥이 평평한 알약 위에 동그라미 둘.</summary>
        private static Color CloudPixel(int x, int y, int w, int h)
        {
            var p = new Vector2(x + 0.5f, y + 0.5f);
            float d = float.MaxValue;
            // 알약 (아래쪽 몸통)
            float px = Mathf.Clamp(p.x, h * 0.3f, w - h * 0.3f);
            d = Mathf.Min(d, Vector2.Distance(p, new Vector2(px, h * 0.3f)) - h * 0.26f);
            d = Mathf.Min(d, Vector2.Distance(p, new Vector2(w * 0.36f, h * 0.5f)) - h * 0.36f);
            d = Mathf.Min(d, Vector2.Distance(p, new Vector2(w * 0.6f, h * 0.48f)) - h * 0.28f);
            if (p.y < h * 0.05f) d = Mathf.Max(d, h * 0.05f - p.y);
            return new Color(1, 1, 1, Mathf.Clamp01(0.5f - d));
        }

        /// <summary>
        /// 평상: 위가 좁은 사다리꼴(원근) 나무판. 세로 판자 줄, 앞으로 올수록 진해지는 색, 위 모서리의 밝은 띠.
        /// </summary>
        private static Color TablePixel(int x, int y, int w, int h)
        {
            float v = y / (float)(h - 1);          // 0 = 앞(아래), 1 = 뒤(위)
            float inset = 0.07f * w * v;            // 뒤로 갈수록 좁아진다
            float alpha = Mathf.Clamp01(Mathf.Min(x + 0.5f - inset, w - inset - x - 0.5f) + 0.5f);
            if (alpha <= 0f) return Color.clear;

            var back = new Color(0.784f, 0.573f, 0.384f);
            var front = new Color(0.71f, 0.494f, 0.314f);
            var c = Color.Lerp(front, back, v);

            // 판자 줄: 원근에 맞춰 뒤쪽에서 모이도록 x를 사다리꼴 안의 비율로 바꿔서 나눈다.
            float u = (x - inset) / (w - inset * 2f);
            float plank = Mathf.Repeat(u * 6f, 1f);
            if (plank < 0.006f * 6f || plank > 1f - 0.004f * 6f) c *= 0.86f;
            // 판자마다 아주 살짝 다른 색
            int index = Mathf.FloorToInt(u * 6f);
            c *= 1f + ((index * 37) % 5 - 2) * 0.012f;

            // 위 모서리의 밝은 띠 (탁자 두께)
            if (h - y < 12) c = new Color(0.863f, 0.682f, 0.498f);
            c.a = alpha;
            return c;
        }

        private static Color SlantTagPixel(int x, int y, int w, int h)
        {
            // 오른쪽 끝: 위는 끝까지, 아래로 갈수록 20px 안쪽으로
            float right = w - 20f * (1f - y / (float)(h - 1));
            return new Color(1, 1, 1, Mathf.Clamp01(right - (x + 0.5f) + 0.5f));
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
