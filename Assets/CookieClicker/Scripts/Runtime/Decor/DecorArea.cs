using System.Collections.Generic;
using CookieClicker.Core;
using UnityEngine;

namespace CookieClicker.Decor
{
    /// <summary>놓을 위치를 계산한 결과. Valid가 false면 Reason에 이유가 들어 있다.</summary>
    public struct DecorPlacement
    {
        public Vector3 LocalPosition;
        public float Yaw;
        public string Slot;
        public bool Valid;
        public string Reason;
    }

    /// <summary>
    /// "내 평상 부지": 꾸미기 물건을 놓을 수 있는 격자 땅. 이 오브젝트의 위치가 격자의 왼쪽 아래 모서리(0,0)다.
    /// 칸(cellSize) 단위로 바닥은 칸 가운데, 벽은 테두리 선, 기둥은 꼭짓점에 붙고, 가구는 freeStep 간격으로 놓인다.
    /// 놓은 물건은 GameState.Decor에 기록되어 클리커 세이브와 같이 저장된다.
    /// </summary>
    public class DecorArea : MonoBehaviour
    {
        [SerializeField] private CookieGame game;
        [SerializeField] private DecorCatalog catalog;

        [Header("격자")]
        [SerializeField] private float cellSize = 2f;
        [Tooltip("한 층 높이 (벽 높이)")]
        [SerializeField] private float storeyHeight = 2.5f;
        [Tooltip("가로(x), 세로(z) 칸 수")]
        [SerializeField] private Vector2Int cells = new Vector2Int(12, 10);
        [SerializeField, Min(1)] private int maxLevels = 3;
        [Tooltip("가구를 놓는 작은 격자 간격 (m)")]
        [SerializeField] private float freeStep = 0.5f;

        [Header("씬 연결")]
        [Tooltip("놓은 물건이 들어가는 곳")]
        [SerializeField] private Transform placedRoot;
        [Tooltip("꾸미기 모드일 때만 보이는 격자 선")]
        [SerializeField] private GameObject gridVisual;
        [Tooltip("놓거나 회수할 때 튀는 먼지 (씬에 하나만 두고 위치를 옮겨 가며 쏜다)")]
        [SerializeField] private ParticleSystem dust;
        [SerializeField] private int dustCount = 16;

        private readonly Dictionary<string, PlacedDecor> slots = new Dictionary<string, PlacedDecor>();
        private readonly Dictionary<DecorPiece, List<Box>> boxCache = new Dictionary<DecorPiece, List<Box>>();
        private readonly Collider[] overlapBuffer = new Collider[16];

        public DecorCatalog Catalog => catalog;
        public float CellSize => cellSize;

        private void Start()
        {
            Rebuild();
            if (gridVisual != null) gridVisual.SetActive(false);
            game.ProgressReset += Rebuild;
        }

        private void OnDestroy()
        {
            if (game != null) game.ProgressReset -= Rebuild;
        }

        public void SetGridVisible(bool visible)
        {
            if (gridVisual != null) gridVisual.SetActive(visible);
        }

        /// <summary>점이 부지 위(가로·세로 범위 안)에 있는지. 높이는 보지 않는다.</summary>
        public bool Contains(Vector3 worldPoint)
        {
            var local = transform.InverseTransformPoint(worldPoint);
            return local.x >= 0f && local.z >= 0f && local.x <= cells.x * cellSize && local.z <= cells.y * cellSize;
        }

        // ------------------------------------------------------------------ 위치 계산

        /// <summary>바라보는 지점(hit)에 piece를 놓으면 어디에 붙는지 계산하고, 놓을 수 있는지 검사한다.</summary>
        public DecorPlacement GetPlacement(DecorPiece piece, RaycastHit hit, int rotationSteps)
        {
            var result = new DecorPlacement();
            Vector3 local = transform.InverseTransformPoint(hit.point);
            Vector3 normal = transform.InverseTransformDirection(hit.normal);
            bool onTop = normal.y > 0.7f;

            // 윗면을 보면 그 높이의 층, 옆면(벽)을 보면 맞은 높이의 층.
            int level = Mathf.Max(0, onTop
                ? Mathf.FloorToInt((local.y + 0.3f) / storeyHeight)
                : Mathf.FloorToInt(local.y / storeyHeight));
            float baseY = level * storeyHeight;
            // 옆면을 보고 있으면 면 바깥쪽 칸에 붙인다 (벽 옆에 바닥 이어 붙이기).
            Vector3 p = onTop ? local : local + normal * (cellSize * 0.25f);
            float fx = p.x / cellSize, fz = p.z / cellSize;
            string group = piece.SlotGroup;
            bool inside;

            switch (piece.Snap)
            {
                case DecorSnap.Floor:
                {
                    int i = Mathf.FloorToInt(fx), j = Mathf.FloorToInt(fz);
                    result.LocalPosition = new Vector3((i + 0.5f) * cellSize, baseY, (j + 0.5f) * cellSize);
                    result.Yaw = rotationSteps * 90f;
                    result.Slot = $"{group}:{i}:{j}:{level}";
                    inside = i >= 0 && j >= 0 && i < cells.x && j < cells.y;
                    if (inside && level > 0 && !FloorSupported(i, j, level)) return Fail(result, Loc.T("받칠 기둥이나 벽이 없어요", "Needs a pillar or wall below"));
                    break;
                }
                case DecorSnap.Wall:
                {
                    // 가까운 테두리 선에 붙는다. 회전(R)은 앞뒤만 뒤집는다 (창문·문 방향).
                    float dx = Mathf.Abs(fx - Mathf.Round(fx)), dz = Mathf.Abs(fz - Mathf.Round(fz));
                    bool alongX = dz <= dx;
                    int i = alongX ? Mathf.FloorToInt(fx) : Mathf.RoundToInt(fx);
                    int j = alongX ? Mathf.RoundToInt(fz) : Mathf.FloorToInt(fz);
                    result.LocalPosition = alongX
                        ? new Vector3((i + 0.5f) * cellSize, baseY, j * cellSize)
                        : new Vector3(i * cellSize, baseY, (j + 0.5f) * cellSize);
                    result.Yaw = (alongX ? 0f : 90f) + (rotationSteps % 2 == 1 ? 180f : 0f);
                    char axis = alongX ? 'x' : 'z';
                    result.Slot = $"{group}:{axis}{i}:{j}:{level}";
                    inside = alongX
                        ? i >= 0 && i < cells.x && j >= 0 && j <= cells.y
                        : j >= 0 && j < cells.y && i >= 0 && i <= cells.x;
                    if (inside && level > 0 && !WallSupported(axis, i, j, level)) return Fail(result, Loc.T("받칠 바닥이나 벽이 없어요", "Needs a floor or wall below"));
                    break;
                }
                case DecorSnap.Pillar:
                {
                    int i = Mathf.RoundToInt(fx), j = Mathf.RoundToInt(fz);
                    result.LocalPosition = new Vector3(i * cellSize, baseY, j * cellSize);
                    result.Yaw = rotationSteps * 90f;
                    result.Slot = $"{group}:{i}:{j}:{level}";
                    inside = i >= 0 && j >= 0 && i <= cells.x && j <= cells.y;
                    if (inside && level > 0 && !PillarSupported(i, j, level)) return Fail(result, Loc.T("받칠 바닥이나 기둥이 없어요", "Needs a floor or pillar below"));
                    break;
                }
                default:
                {
                    if (!onTop) return Fail(result, Loc.T("바닥을 바라봐 주세요", "Look at the floor"));
                    result.LocalPosition = new Vector3(Snap(local.x), local.y, Snap(local.z));
                    result.Yaw = rotationSteps * 90f;
                    result.Slot = string.Empty;
                    var lp = result.LocalPosition;
                    inside = lp.x >= 0f && lp.z >= 0f && lp.x <= cells.x * cellSize && lp.z <= cells.y * cellSize;
                    break;
                }
            }

            if (!inside) return Fail(result, Loc.T("평상 부지 안에만 놓을 수 있어요", "Only inside your lot"));
            if (level >= maxLevels) return Fail(result, Loc.T("더 높이는 못 쌓아요", "Too high"));
            if (!string.IsNullOrEmpty(result.Slot) && slots.ContainsKey(result.Slot)) return Fail(result, Loc.T("이미 놓여 있어요", "Already taken"));
            if (Overlaps(piece, result.LocalPosition, result.Yaw, out bool hitPlayer))
                return Fail(result, hitPlayer ? Loc.T("내가 서 있는 자리예요", "You're standing there") : Loc.T("다른 물건과 겹쳐요", "Something is in the way"));

            result.Valid = true;
            return result;
        }

        private float Snap(float value) => Mathf.Round(value / freeStep) * freeStep;

        private static DecorPlacement Fail(DecorPlacement placement, string reason)
        {
            placement.Valid = false;
            placement.Reason = reason;
            return placement;
        }

        // 위층은 아래에 받침이 있어야 놓을 수 있다 (공중에 둥둥 뜨지 않게).
        private bool FloorSupported(int i, int j, int level) =>
            Has($"Pillar:{i}:{j}:{level - 1}") || Has($"Pillar:{i + 1}:{j}:{level - 1}") ||
            Has($"Pillar:{i}:{j + 1}:{level - 1}") || Has($"Pillar:{i + 1}:{j + 1}:{level - 1}") ||
            Has($"Wall:x{i}:{j}:{level - 1}") || Has($"Wall:x{i}:{j + 1}:{level - 1}") ||
            Has($"Wall:z{i}:{j}:{level - 1}") || Has($"Wall:z{i + 1}:{j}:{level - 1}") ||
            // 옆 칸에 이미 바닥이 있으면 이어 붙일 수 있다 (발코니처럼).
            Has($"Floor:{i - 1}:{j}:{level}") || Has($"Floor:{i + 1}:{j}:{level}") ||
            Has($"Floor:{i}:{j - 1}:{level}") || Has($"Floor:{i}:{j + 1}:{level}");

        private bool WallSupported(char axis, int i, int j, int level) =>
            Has($"Wall:{axis}{i}:{j}:{level - 1}") ||
            Has($"Floor:{i}:{j}:{level}") ||
            (axis == 'x' ? Has($"Floor:{i}:{j - 1}:{level}") : Has($"Floor:{i - 1}:{j}:{level}"));

        private bool PillarSupported(int i, int j, int level) =>
            Has($"Pillar:{i}:{j}:{level - 1}") ||
            Has($"Floor:{i}:{j}:{level}") || Has($"Floor:{i - 1}:{j}:{level}") ||
            Has($"Floor:{i}:{j - 1}:{level}") || Has($"Floor:{i - 1}:{j - 1}:{level}");

        private bool Has(string slot) => slots.ContainsKey(slot);

        /// <summary>
        /// 놓을 자리에 다른 것이 있는지. 바닥·벽·기둥끼리는 칸(slot)으로 겹침을 막으므로 여기서는 무시하고,
        /// 가구·플레이어·건물처럼 칸이 없는 것과 겹치는지만 본다.
        /// </summary>
        private bool Overlaps(DecorPiece piece, Vector3 localPosition, float yaw, out bool hitPlayer)
        {
            hitPlayer = false;
            // 러그처럼 바닥에 까는 것은 겹쳐도 된다 (가구 밑에 깔기, 러그 여러 장 겹치기).
            if (piece.Flat) return false;
            var rotation = transform.rotation * Quaternion.Euler(0f, yaw, 0f);
            Vector3 origin = transform.TransformPoint(localPosition);

            // 프리팹의 충돌 상자 하나하나를 검사한다 (전체를 감싸는 큰 상자로 보면 파라솔 밑에 의자를 못 놓는다).
            foreach (var box in Boxes(piece))
            {
                // 살짝 줄여서 바로 옆에 붙이거나 바닥 위에 올리는 건 겹침으로 치지 않는다.
                Vector3 half = Vector3.Max(box.Half - Vector3.one * 0.05f, Vector3.one * 0.01f);
                int count = Physics.OverlapBoxNonAlloc(origin + rotation * box.Center, half, overlapBuffer,
                    rotation * box.Rotation, ~0, QueryTriggerInteraction.Ignore);
                for (int k = 0; k < count; k++)
                {
                    var other = overlapBuffer[k].GetComponentInParent<PlacedDecor>();
                    if (other != null && other.Piece.Flat) continue;
                    if (other != null && piece.Snap != DecorSnap.Free && other.Piece.Snap != DecorSnap.Free) continue;
                    hitPlayer = overlapBuffer[k] is CharacterController;
                    return true;
                }
            }
            return false;
        }

        private struct Box
        {
            public Vector3 Center;
            public Vector3 Half;
            public Quaternion Rotation;
        }

        /// <summary>프리팹의 BoxCollider들을 프리팹 기준 좌표로 모아 둔다. 프리팹을 씬에 꺼내지 않고 에셋에서 바로 읽는다.</summary>
        private List<Box> Boxes(DecorPiece piece)
        {
            if (boxCache.TryGetValue(piece, out var cached)) return cached;

            var root = piece.Prefab.transform;
            var boxes = new List<Box>();
            foreach (var collider in piece.Prefab.GetComponentsInChildren<BoxCollider>())
            {
                var t = collider.transform;
                var scale = t.lossyScale;
                boxes.Add(new Box
                {
                    Center = root.InverseTransformPoint(t.TransformPoint(collider.center)),
                    Half = Vector3.Scale(collider.size * 0.5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))),
                    Rotation = Quaternion.Inverse(root.rotation) * t.rotation,
                });
            }
            boxCache[piece] = boxes;
            return boxes;
        }

        // ------------------------------------------------------------------ 놓기·회수

        /// <param name="color">칠할 색 번호 (catalog 팔레트, -1이면 원래 색)</param>
        public PlacedDecor Place(DecorPiece piece, DecorPlacement placement, int color = -1)
        {
            var record = new DecorRecord { id = piece.Id, slot = placement.Slot, position = placement.LocalPosition, yaw = placement.Yaw, color = color };
            game.State.Decor.Add(record);
            var placed = Spawn(piece, record, true);
            PlayDust(placed.transform.position);
            return placed;
        }

        /// <summary>이미 놓인 물건의 색을 바꾼다 (세이브에도 기록).</summary>
        public void Paint(PlacedDecor placed, int color)
        {
            placed.Record.color = color;
            placed.ApplyColor(catalog.PaintColor(color));
            PlayDust(placed.transform.position);
        }

        public void Remove(PlacedDecor placed)
        {
            game.State.Decor.Remove(placed.Record);
            if (!string.IsNullOrEmpty(placed.Record.slot)) slots.Remove(placed.Record.slot);
            PlayDust(placed.transform.position);
            placed.PlayRemove();
        }

        /// <summary>세이브에 기록된 대로 물건을 다시 놓는다 (시작할 때, 진행 초기화했을 때).</summary>
        public void Rebuild()
        {
            slots.Clear();
            for (int i = placedRoot.childCount - 1; i >= 0; i--) Destroy(placedRoot.GetChild(i).gameObject);

            var records = game.State.Decor;
            for (int i = records.Count - 1; i >= 0; i--)
            {
                var piece = catalog.Find(records[i].id);
                // 목록에서 지운 물건은 세이브에서도 정리한다.
                if (piece == null || piece.Prefab == null) records.RemoveAt(i);
                else Spawn(piece, records[i], false);
            }
        }

        private PlacedDecor Spawn(DecorPiece piece, DecorRecord record, bool animate)
        {
            var go = Instantiate(piece.Prefab, placedRoot);
            go.name = piece.Prefab.name;
            go.transform.localPosition = record.position;
            go.transform.localRotation = Quaternion.Euler(0f, record.yaw, 0f);
            var placed = go.GetComponent<PlacedDecor>();
            if (placed == null) placed = go.AddComponent<PlacedDecor>();
            placed.Init(piece, record, animate);
            placed.ApplyColor(catalog.PaintColor(record.color));
            if (!string.IsNullOrEmpty(record.slot)) slots[record.slot] = placed;
            return placed;
        }

        private void PlayDust(Vector3 position)
        {
            if (dust == null) return;
            dust.transform.position = position;
            dust.Emit(dustCount);
        }
    }
}
