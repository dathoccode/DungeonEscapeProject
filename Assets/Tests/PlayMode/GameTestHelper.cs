using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DungeonEscape.Tests
{
    /// <summary>
    /// Hàm dùng chung cho test tự động của Dungeon Escape 2D.
    ///
    /// Script game nằm trong Assembly-CSharp (không có assembly definition) nên test không gọi
    /// trực tiếp được class của game. Các hàm dưới đây truy cập class, field, hàm của game qua tên
    /// (reflection), nhờ vậy không phải thay đổi cấu trúc project.
    /// </summary>
    public static class G
    {
        // Build index theo File > Build Profiles: 0 Start Screen, 1..6 Level1..Level6, 7 End Screen
        public const int StartScreen = 0;
        public const int Level1 = 1;
        public const int Level2 = 2;

        private const BindingFlags All =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        // ================= Truy cập class của game =================

        public static Type FindType(string name)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType(name);
                if (t != null) return t;
            }
            Assert.Fail($"Không tìm thấy class '{name}' trong project.");
            return null;
        }

        public static object Get(object target, string member)
        {
            for (Type t = target.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(member, All | BindingFlags.DeclaredOnly);
                if (f != null) return f.GetValue(target);
                PropertyInfo p = t.GetProperty(member, All | BindingFlags.DeclaredOnly);
                if (p != null) return p.GetValue(target);
            }
            Assert.Fail($"'{target.GetType().Name}' không có field/property '{member}'");
            return null;
        }

        public static object Call(object target, string method, params object[] args)
        {
            for (Type t = target.GetType(); t != null; t = t.BaseType)
            {
                MethodInfo m = t.GetMethod(method, All | BindingFlags.DeclaredOnly);
                if (m != null) return m.Invoke(target, args.Length == 0 && m.GetParameters().Length > 0
                    ? DefaultArgs(m) : args);
            }
            Assert.Fail($"'{target.GetType().Name}' không có hàm '{method}'");
            return null;
        }

        private static object[] DefaultArgs(MethodInfo m)
        {
            ParameterInfo[] ps = m.GetParameters();
            object[] a = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++) a[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
            return a;
        }

        public static Component Comp(GameObject go, string typeName)
        {
            Component c = go.GetComponent(FindType(typeName));
            Assert.IsNotNull(c, $"'{go.name}' không có component {typeName}");
            return c;
        }

        /// <summary>Tìm 1 component trong scene. Trả về null nếu không có.</summary>
        public static Component TryFind(string typeName, bool includeInactive = true) =>
            Object.FindAnyObjectByType(FindType(typeName),
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude) as Component;

        public static Component Find(string typeName)
        {
            Component c = TryFind(typeName);
            Assert.IsNotNull(c, $"Scene hiện tại không có {typeName}");
            return c;
        }

        public static Object[] FindAll(string typeName) =>
            Object.FindObjectsByType(FindType(typeName), FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        /// <summary>Đọc chữ của TextMeshProUGUI / Text (field kiểu TMP nằm trong script game).</summary>
        public static string TextOf(object textComponent) => (string)Get(textComponent, "text");

        public static bool CanvasEnabled(object menu, string canvasField) => ((Canvas)Get(menu, canvasField)).enabled;

        // ================= PlayerPrefs (dữ liệu lưu của game) =================

        private static readonly string[] IntKeys =
        {
            "Level1", "Level2", "Level3", "Level4", "Level5", "Level6",
            "LifeCount", "Foods", "Character", "LatestCheckpointIndex"
        };
        private static readonly string[] FloatKeys = { "MusicVolume", "EffectsVolume" };
        private static readonly Dictionary<string, object> backup = new Dictionary<string, object>();

        /// <summary>Sao lưu dữ liệu chơi thật trên máy trước khi chạy test.</summary>
        public static void BackupPrefs()
        {
            backup.Clear();
            foreach (string k in IntKeys) if (PlayerPrefs.HasKey(k)) backup[k] = PlayerPrefs.GetInt(k);
            foreach (string k in FloatKeys) if (PlayerPrefs.HasKey(k)) backup[k] = PlayerPrefs.GetFloat(k);
        }

        /// <summary>Trả lại dữ liệu chơi thật sau khi chạy test.</summary>
        public static void RestorePrefs()
        {
            foreach (string k in IntKeys) PlayerPrefs.DeleteKey(k);
            foreach (string k in FloatKeys) PlayerPrefs.DeleteKey(k);
            foreach (KeyValuePair<string, object> kv in backup)
            {
                if (kv.Value is int i) PlayerPrefs.SetInt(kv.Key, i);
                else PlayerPrefs.SetFloat(kv.Key, (float)kv.Value);
            }
            PlayerPrefs.Save();
        }

        /// <summary>Đưa dữ liệu về như người chơi mới: chỉ Level1 mở, 3 mạng, 0 Foods, nhân vật mặc định.</summary>
        public static void FreshProgress(int lives = 3, int foods = 0, int character = 0)
        {
            PlayerPrefs.SetInt("Level1", 1);   // 1 = Unlocked
            for (int i = 2; i <= 6; i++) PlayerPrefs.SetInt("Level" + i, 0);   // 0 = Locked
            PlayerPrefs.SetInt("LifeCount", lives);
            PlayerPrefs.SetInt("Foods", foods);
            PlayerPrefs.SetInt("Character", character);
            PlayerPrefs.SetInt("LatestCheckpointIndex", 0);
            PlayerPrefs.Save();
        }

        public static int Lives => PlayerPrefs.GetInt("LifeCount", -1);

        // ================= Scene =================

        /// <summary>Nạp scene theo build index (an toàn hơn theo tên vì tên file level1 viết thường).</summary>
        public static IEnumerator LoadScene(int buildIndex)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(buildIndex);
            yield return WaitUntil(() => SceneManager.GetActiveScene().buildIndex == buildIndex, 10f);
            Assert.AreEqual(buildIndex, SceneManager.GetActiveScene().buildIndex,
                $"Không nạp được scene có build index {buildIndex}. Kiểm tra Build Profiles / Build Settings.");
            yield return null;
            yield return new WaitForFixedUpdate();
        }

        public static int ActiveIndex => SceneManager.GetActiveScene().buildIndex;

        /// <summary>Chờ tới khi điều kiện đúng. Dùng thời gian thực nên vẫn chạy khi game bị Pause (timeScale = 0).</summary>
        public static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            float t = 0f;
            while (!condition() && t < timeoutSeconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // ================= Player =================

        public static GameObject Player()
        {
            GameObject p = GameObject.Find("Player");
            Assert.IsNotNull(p, "Không tìm thấy object tên 'Player' trong scene");
            return p;
        }

        public static void Teleport(GameObject player, Vector2 pos)
        {
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            player.transform.position = pos;
            rb.position = pos;
            rb.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        /// <summary>PlayerLife.Die() tắt BoxCollider2D; Respawn() bật lại. Dùng để nhận biết đang chết hay đã hồi sinh.</summary>
        public static bool ColliderOn(GameObject player) => player.GetComponent<BoxCollider2D>().enabled;

        /// <summary>Tạo một bẫy (script Trap của game) ngay tại vị trí Player để Player chết.</summary>
        public static GameObject SpawnTrapOn(GameObject player)
        {
            GameObject trap = new GameObject("TestTrap");
            trap.transform.position = player.GetComponent<Collider2D>().bounds.center;
            BoxCollider2D c = trap.AddComponent<BoxCollider2D>();
            c.isTrigger = true;
            c.size = Vector2.one * 0.5f;
            trap.AddComponent(FindType("Trap"));
            return trap;
        }

        /// <summary>Cho Player chết bằng bẫy rồi chờ tới khi hồi sinh xong hoặc Game Over hiện ra.</summary>
        public static IEnumerator KillPlayer(GameObject player)
        {
            GameObject trap = SpawnTrapOn(player);
            yield return WaitUntil(() => !ColliderOn(player), 2f);
            Assert.IsFalse(ColliderOn(player), "Chạm bẫy nhưng Player không chết");
            Object.Destroy(trap);
            // Animation chết gọi CheckLifeStatus(): hồi sinh (bật lại collider) hoặc mở Game Over (timeScale = 0)
            yield return WaitUntil(() => ColliderOn(player) || Time.timeScale == 0f, 6f);
        }
    }

    /// <summary>Lớp cha cho các test không cần giả lập bàn phím.</summary>
    public abstract class GameTestBase
    {
        [OneTimeSetUp] public void SaveRealPrefs() => G.BackupPrefs();
        [OneTimeTearDown] public void RestoreRealPrefs() => G.RestorePrefs();

        [SetUp] public void FreshData() => G.FreshProgress();

        [TearDown]
        public void Cleanup()
        {
            Time.timeScale = 1f;
            LogAssert.ignoreFailingMessages = false;
        }
    }
}
