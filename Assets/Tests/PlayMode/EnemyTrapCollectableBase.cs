using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DungeonEscape.Tests
{
    /// <summary>
    /// Lớp cha cho HazardTests (TC-HZ), EnemyTests (TC-EN), CollectableTests (TC-CL) – task P1-32.
    ///
    /// - Sân thử dựng trong Level2 (màn không có enemy), ở xa map (x = 1000, y = 300), dùng prefab thật của game
    ///   nạp qua AssetDatabase (chỉ chạy trong Editor).
    /// - Test kiểm tra vị trí đặt trong màn dùng Level1..Level3 thật (master 8683117 đã xóa Level4..Level7).
    /// - Giống PlayerStateTests: InputTestFixture (chặn bàn phím thật), sao lưu/khôi phục PlayerPrefs,
    ///   hủy nút cảm ứng, TearDown hủy object của scene trước base.TearDown(), chờ bằng đồng hồ thực.
    /// - Không sửa file nào của game: mọi object thử nghiệm chỉ tạo lúc chạy test.
    /// </summary>
    public abstract class ECTestBase : InputTestFixture
    {
        // Build index theo EditorBuildSettings (master 8683117): 1 Level1, 2 Level2, 3 Level3.
        // Index 4, 5 trỏ tới Level4/Level5 đã bị xóa khỏi project nên không dùng.
        protected const int Level1 = 1, Level2 = 2, Level3 = 3;
        protected static readonly int[] PlayableLevels = { Level1, Level2, Level3 };

        protected const float ArenaX = 1000f, ArenaY = 300f;

        protected const string SpikesPrefab = "Assets/Prefabs/Environment/Traps/Spikes.prefab";
        protected const string FirePrefab = "Assets/Prefabs/Environment/Traps/Fire.prefab";
        protected const string MushroomPrefab = "Assets/Prefabs/Enemies/CompletePrefabs/CompleteMushroom.prefab";
        protected const string AngryPigPrefab = "Assets/Prefabs/Enemies/CompletePrefabs/CompleteAngryPig.prefab";
        protected const string CherryPrefab = "Assets/Prefabs/Environment/Collectibles/Cherry.prefab";
        protected const string HeartPrefab = "Assets/Prefabs/Environment/Collectibles/Heart.prefab";
        protected const string KeyPrefab = "Assets/Prefabs/Environment/Collectibles/Key.prefab";

        /// <summary>Các script gây chết của game (Trap gồm cả SawTrap, FireTrap là lớp con).</summary>
        protected static readonly string[] HazardTypes = { "SpikeTrap", "SawTrap", "FireTrap", "Trap", "DeathZone" };

        protected GameObject player;
        protected Rigidbody2D rb;
        protected BoxCollider2D box;
        protected Component life, collector;

        /// <summary>Lỗi / exception game ghi ra Console trong lúc test (dùng để báo NRE rõ ràng hơn).</summary>
        protected readonly List<string> errors = new List<string>();

        [OneTimeSetUp] public void SaveRealPrefs() => G.BackupPrefs();
        [OneTimeTearDown] public void RestoreRealPrefs() => G.RestorePrefs();

        public override void Setup()
        {
            base.Setup();
            G.FreshProgress();
            InputSystem.AddDevice<Keyboard>();
            errors.Clear();
            Application.logMessageReceived += OnLog;
        }

        public override void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            Time.timeScale = 1f;
            LogAssert.ignoreFailingMessages = false;
            DestroySceneObjects();
            base.TearDown();
        }

        private void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                string where = (stack ?? "").Split('\n')[0].Trim();
                errors.Add(msg.Split('\n')[0] + (where != "" ? $" @ {where}" : ""));
            }
        }

        protected string Errors => errors.Count == 0 ? "không có" : string.Join(" | ", errors);

        /// <summary>
        /// Lỗi game đã biết, xảy ra ngay khi nạp màn và đã có test riêng ghi nhận (TC_CL_09):
        /// ItemCollector.keysText chưa được gán → NRE ở ItemCollector.UpdateKeys (ItemCollector.cs:45).
        /// Các test khác bỏ qua đúng lỗi này để không bị nó che mất kết quả.
        /// </summary>
        protected static bool IsKnownLoadBug(string e) => e.Contains("ItemCollector.UpdateKeys");

        protected List<string> UnexpectedErrors => errors.FindAll(e => !IsKnownLoadBug(e));

        /// <summary>
        /// Nạp scene, trong lúc nạp tạm bỏ qua lỗi Console để lỗi đã biết (TC_CL_09) không làm Fail test khác;
        /// sau đó kiểm tra không có lỗi nào KHÁC lỗi đã biết.
        /// </summary>
        private IEnumerator LoadTolerant(int buildIndex)
        {
            bool prev = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            int before = errors.Count;
            yield return G.LoadScene(buildIndex);
            yield return WaitFor(() => false, 1f);   // chờ Start() của mọi object chạy xong
            // Hủy nút cảm ứng ảo ngay (giống PlayerStateTests): nếu để tới lúc nạp màn kế tiếp, OnScreenControl bị gỡ
            // khi đang dùng thiết bị ảo của InputTestFixture → ArgumentNullException trong Input System (lỗi môi trường test).
            DestroyAll<OnScreenControl>();
            yield return null;
            LogAssert.ignoreFailingMessages = prev;
            List<string> fresh = errors.GetRange(before, errors.Count - before).FindAll(e => !IsKnownLoadBug(e));
            Assert.IsEmpty(fresh, $"Nạp {SceneName} sinh lỗi: {string.Join(" | ", fresh)}");
        }

        private static void DestroySceneObjects()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
            }
            DestroyAll<OnScreenControl>();
            DestroyAll<PlayerInput>();
            DestroyAll<InputSystemUIInputModule>();
        }

        protected static void DestroyAll<T>() where T : Component
        {
            foreach (T c in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c != null) Object.DestroyImmediate(c.gameObject);
        }

        // ================= Chuẩn bị =================

        /// <summary>Vào một level với số mạng / Foods cho trước, lấy các component của Player.</summary>
        protected IEnumerator EnterLevel(int buildIndex, int lives = 3, int foods = 0)
        {
            PlayerPrefs.SetInt("LifeCount", lives);
            PlayerPrefs.SetInt("Foods", foods);
            yield return LoadTolerant(buildIndex);
            DestroyAll<OnScreenControl>();
            yield return null;
            player = G.Player();
            rb = player.GetComponent<Rigidbody2D>();
            box = player.GetComponent<BoxCollider2D>();
            life = G.Comp(player, "PlayerLife");
            collector = G.Comp(player, "ItemCollector");
        }

        /// <summary>Chỉ nạp scene (cho test kiểm tra vị trí đặt, không cần Player).</summary>
        protected IEnumerator LoadOnly(int buildIndex)
        {
            yield return LoadTolerant(buildIndex);
        }

        protected static string SceneName => SceneManager.GetActiveScene().name;

        /// <summary>
        /// Tắt các enemy bắn đạn (RangedEnemy, EnemyMovement) trong scene – chỉ trong lúc test – để đạn của enemy khác
        /// không làm Player chết giữa chừng. Trả lại component được giữ nguyên (keep), nếu có.
        /// </summary>
        /// <summary>Trunk = RangedEnemy bắn ngang (isAerial = false); Bee là RangedEnemy bắn xuống (isAerial = true).</summary>
        protected static Component FindTrunk()
        {
            foreach (Object o in G.FindAll("RangedEnemy"))
                if (!(bool)G.Get(o, "isAerial")) return (Component)o;
            return null;
        }

        protected static void DisableRangedEnemies(Component keep = null)
        {
            foreach (Object o in G.FindAll("RangedEnemy"))
            {
                Component r = (Component)o;
                if (r == keep) continue;
                ((Behaviour)r).enabled = false;
                if (G.Get(r, "enemyMovement") is Behaviour m) m.enabled = false;
            }
        }

        /// <summary>Nạp prefab thật của game (chỉ có trong Editor).</summary>
        protected static GameObject Spawn(string path, Vector2 pos)
        {
#if UNITY_EDITOR
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"Không tìm thấy prefab {path}");
            GameObject go = Object.Instantiate(prefab, pos, Quaternion.identity);
            Physics2D.SyncTransforms();
            return go;
#else
            Assert.Ignore("Test dùng prefab nên chỉ chạy được trong Unity Editor");
            return null;
#endif
        }

        /// <summary>Sàn đặc trên layer Ground (giống Terrain), mặt trên ở y = top.</summary>
        protected static GameObject Floor(float centerX, float top, float width = 80f)
        {
            GameObject go = new GameObject("TestFloor") { layer = LayerMask.NameToLayer("Ground") };
            go.transform.position = new Vector2(centerX, top - 0.5f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(width, 1f);
            Physics2D.SyncTransforms();
            return go;
        }

        /// <summary>Dời object sao cho đáy collider nằm đúng trên mặt sàn y = top, tâm collider ở x.</summary>
        protected static void PutOnFloor(GameObject go, float x, float top)
        {
            Collider2D c = go.GetComponentInChildren<Collider2D>();
            go.transform.position += new Vector3(x - c.bounds.center.x, top - c.bounds.min.y, 0f);
            Physics2D.SyncTransforms();
        }

        /// <summary>Đặt Player đứng trên sàn thử ở x và chờ đứng yên.</summary>
        protected IEnumerator PlaceOnFloor(float x, float top = ArenaY)
        {
            float feetOffset = player.transform.position.y - box.bounds.min.y;
            G.Teleport(player, new Vector2(x, top + feetOffset + 0.02f));
            yield return WaitFor(() => Mathf.Abs(box.bounds.min.y - top) < 0.08f && Mathf.Abs(rb.linearVelocity.y) < 0.05f, 2f);
            Assert.IsTrue(box.enabled, "Player đang chết, không đặt lên sàn thử được");
        }

        // ================= Vị trí tiếp xúc =================

        /// <summary>Độ lệch từ pivot Player tới tâm BoxCollider2D (offset y = -0.273 trong prefab).</summary>
        protected Vector2 BoxOffset => (Vector2)(box.bounds.center - player.transform.position);

        protected void TeleportCenterTo(Vector2 center) => G.Teleport(player, center - BoxOffset);

        /// <summary>
        /// Vị trí pivot của Player để chạm vào target theo góc angleDeg đo từ phương thẳng đứng
        /// (0° = Player ở ngay trên, 90° = Player ở ngang). Hai collider chồng lên nhau `overlap` đơn vị
        /// để có va chạm ngay ở bước vật lý kế tiếp. EnemyDamage.DotTest so pivot với pivot nên góc được tính
        /// theo pivot: dot = cos(angle).
        /// </summary>
        protected Vector2 ContactPivot(Collider2D target, float angleDeg, float overlap = 0.06f)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            Vector2 u = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
            Vector2 targetPivot = target.transform.position;
            Vector2 c = targetPivot - (Vector2)target.bounds.center + BoxOffset;
            float w = box.bounds.extents.x + target.bounds.extents.x - overlap;
            float h = box.bounds.extents.y + target.bounds.extents.y - overlap;
            for (float d = 8f; d > 0f; d -= 0.005f)
                if (Mathf.Abs(c.x + d * u.x) <= w && Mathf.Abs(c.y + d * u.y) <= h)
                    return targetPivot + d * u;
            return targetPivot;
        }

        // ================= Đọc trạng thái =================

        protected int LifeField => (int)G.Get(life, "LifeCount");
        protected string HudLives => G.TextOf(G.Get(life, "heartsCount"));
        protected string HudFoods => G.TextOf(G.Get(collector, "cherriesText"));
        protected string HudKeys => G.TextOf(G.Get(collector, "keysText"));
        protected int Keys => (int)G.Call(collector, "GetKeys");
        protected bool Alive => box.enabled;

        protected string Now => $"pos=({player.transform.position.x:F2}, {player.transform.position.y:F2}), " +
                                $"collider={box.enabled}, LifeCount(prefs)={G.Lives}, HUD mạng={HudLives}, timeScale={Time.timeScale}";

        // ================= Chờ =================

        /// <summary>Chờ tới khi điều kiện đúng, tối đa timeout giây đồng hồ thực, ít nhất 10 frame.</summary>
        protected static IEnumerator WaitFor(System.Func<bool> condition, float timeout)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int frames = 0;
            while (!condition() && (sw.Elapsed.TotalSeconds < timeout || frames < 10))
            {
                frames++;
                yield return null;
            }
        }

        protected static IEnumerator FixedSteps(int n)
        {
            for (int i = 0; i < n; i++) yield return new WaitForFixedUpdate();
            yield return null;
        }

        /// <summary>Chờ Player chết (PlayerLife.Die tắt BoxCollider2D).</summary>
        protected IEnumerator WaitDied(string why, float timeout = 2f)
        {
            yield return WaitFor(() => !box.enabled, timeout);
            Assert.IsFalse(box.enabled, $"{why}: Player không chết ({Now})");
        }

        /// <summary>Chờ animation chết gọi CheckLifeStatus: hồi sinh (bật collider) hoặc Game Over (timeScale = 0).</summary>
        protected IEnumerator WaitRespawn()
        {
            yield return WaitFor(() => box.enabled || Time.timeScale == 0f, 6f);
            yield return WaitFor(() => false, 0.3f);
        }
    }
}
