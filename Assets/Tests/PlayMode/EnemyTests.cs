using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DungeonEscape.Tests
{
    /// <summary>
    /// Kiểm thử Enemy: TC-EN-01 .. TC-EN-08 (P1-32).
    ///
    /// Bảng quyết định va chạm Player – Enemy (EnemyDamage.OnCollisionEnter2D, AngryPigDamage.Die):
    ///   R1 Player ở trên (dot > 0.25), enemy thường        → enemy chết (tắt collider, xóa sau 1.25s), Player không mất mạng
    ///   R2 Player ở trên, AngryPig chưa nổi giận            → Pig nổi giận (speed 8, idle 0), không chết, Player không mất mạng
    ///   R3 Player ở trên, AngryPig đã nổi giận              → Pig chết
    ///   R4/R5 Player chạm ngang (dot ≤ 0.25)                → Player chết, trừ 1 mạng
    ///   R6 Player ở trên, enemy KHÔNG có object cha         → mong đợi như R1 (thực tế nghi NRE ở EnemyDamage.cs:47)
    ///   R7 enemy đang chết (collider tắt) chạm vào          → không gây hại
    /// Giá trị biên: dot = cos(góc lệch so với phương thẳng đứng) > 0.25 ⇔ góc &lt; 75.5°. Thử 70° và 80°.
    ///
    /// Sân thử: enemy được đặt lơ lửng (Rigidbody2D Kinematic, tắt EnemyMovement) để Player không chạm sàn
    /// cùng lúc – nếu không, sàn đẩy Player lên làm lệch góc tiếp xúc.
    /// </summary>
    public class EnemyTests : ECTestBase
    {
        private GameObject enemyRoot;
        private Component enemy;          // EnemyDamage hoặc AngryPigDamage
        private Collider2D enemyCol;
        private Behaviour enemyMove;

        /// <summary>Đặt enemy prefab thật lơ lửng cao 3 đơn vị trên sàn thử, đứng yên.</summary>
        private Component SpawnEnemy(string prefab, float x)
        {
            enemyRoot = Spawn(prefab, new Vector2(x, ArenaY + 3f));
            enemy = enemyRoot.GetComponentInChildren(G.FindType("EnemyDamage"));
            Assert.IsNotNull(enemy, $"{prefab} không có EnemyDamage");
            enemyCol = enemy.GetComponent<Collider2D>();
            enemyMove = (Behaviour)enemy.GetComponent(G.FindType("EnemyMovement"));
            enemyMove.enabled = false;
            Rigidbody2D erb = enemy.GetComponent<Rigidbody2D>();
            erb.bodyType = RigidbodyType2D.Kinematic;
            erb.linearVelocity = Vector2.zero;
            enemy.transform.position = new Vector3(x, ArenaY + 3f, 0f);
            Physics2D.SyncTransforms();
            return enemy;
        }

        private IEnumerator Arena(int lives = 3)
        {
            yield return EnterLevel(Level2, lives: lives);
            Floor(ArenaX, ArenaY);
            yield return PlaceOnFloor(ArenaX - 8f);
        }

        /// <summary>Đưa Player tới vị trí chạm enemy theo góc (0° = giẫm thẳng từ trên).</summary>
        private void Touch(Collider2D target, float angleDeg) => G.Teleport(player, ContactPivot(target, angleDeg));

        // R1: giẫm Mushroom từ trên → Mushroom chết và bị xóa, Player không mất mạng
        [UnityTest, Timeout(60000)]
        public IEnumerator TC_EN_01_Mushroom_GiamTuTren_EnemyChet_KhongMatMang()
        {
            yield return Arena();
            SpawnEnemy(MushroomPrefab, ArenaX);

            Touch(enemyCol, 0f);
            yield return WaitFor(() => !enemyCol.enabled || !Alive, 1f);
            Assert.IsTrue(Alive, $"Giẫm Mushroom từ trên nhưng Player lại chết ({Now})");
            Assert.IsFalse(enemyCol.enabled, "Giẫm Mushroom nhưng collider của Mushroom vẫn bật");

            yield return WaitFor(() => enemyRoot == null, 2.5f);
            Assert.IsTrue(enemyRoot == null, "Mushroom chết nhưng không bị xóa sau 2.5s (thiết kế: 1.25s)");
            Assert.AreEqual(3, G.Lives, "Giẫm enemy mà vẫn bị trừ mạng");
            Assert.AreEqual("3", HudLives, "HUD mạng thay đổi khi giẫm enemy");
            Assert.IsEmpty(UnexpectedErrors, $"Có lỗi khi giẫm Mushroom: {Errors}");
        }

        // R4: chạm ngang Mushroom → Player chết, trừ 1 mạng; Mushroom không sao
        [UnityTest, Timeout(60000)]
        public IEnumerator TC_EN_02_Mushroom_ChamNgang_PlayerChetTru1Mang()
        {
            yield return Arena();
            SpawnEnemy(MushroomPrefab, ArenaX);

            Touch(enemyCol, 90f);
            yield return WaitDied("Chạm ngang Mushroom");
            Assert.IsTrue(enemyCol.enabled, "Player chạm ngang nhưng Mushroom lại chết");
            yield return WaitRespawn();
            Assert.AreEqual(2, G.Lives, "Chạm ngang enemy: số mạng không giảm đúng 1 (3 → 2)");
            Assert.AreEqual("2", HudLives, "HUD không cập nhật số mạng");
            Assert.IsTrue(enemyRoot != null && enemyCol.enabled, "Mushroom bị xóa dù Player chạm ngang");
        }

        // Giá trị biên của DotTest (dot > 0.25 ⇔ lệch < 75.5°): 70° (dot 0.34) phải giẫm được, 80° (dot 0.17) Player chết
        [UnityTest, Timeout(60000)]
        public IEnumerator TC_EN_03_GocGiam_70DoGietDuoc_80DoBiGiet()
        {
            yield return Arena();

            // 70°: trong ngưỡng
            SpawnEnemy(MushroomPrefab, ArenaX);
            Collider2D e70 = enemyCol;
            Touch(e70, 70f);
            yield return WaitFor(() => !e70.enabled || !Alive, 1f);
            Assert.IsTrue(Alive, $"Chạm ở góc 70° (dot = 0.34 > 0.25) nhưng Player chết ({Now})");
            Assert.IsFalse(e70.enabled, "Chạm ở góc 70° (dot = 0.34 > 0.25) nhưng Mushroom không chết");

            // 80°: ngoài ngưỡng
            yield return WaitFor(() => false, 0.5f);
            yield return PlaceOnFloor(ArenaX - 8f);
            SpawnEnemy(MushroomPrefab, ArenaX + 10f);
            Collider2D e80 = enemyCol;
            Touch(e80, 80f);
            yield return WaitFor(() => !e80.enabled || !Alive, 1f);
            Assert.IsFalse(Alive, "Chạm ở góc 80° (dot = 0.17 ≤ 0.25) nhưng Player không chết");
            Assert.IsTrue(e80.enabled, "Chạm ở góc 80° (dot = 0.17 ≤ 0.25) nhưng Mushroom lại chết");
            yield return WaitRespawn();
            Assert.AreEqual(2, G.Lives, "Chỉ chết 1 lần (ở góc 80°) nhưng số mạng không phải 2");
        }

        // R2: giẫm AngryPig lần 1 → nổi giận (tăng tốc 8, idle 0, bật AngryMod), chưa chết, Player không mất mạng
        [UnityTest, Timeout(60000)]
        public IEnumerator TC_EN_04_AngryPig_GiamLan1_NoiGianTangToc()
        {
            yield return Arena();
            SpawnEnemy(AngryPigPrefab, ArenaX);

            Touch(enemyCol, 0f);
            yield return WaitFor(() => (float)G.Get(enemyMove, "speed") > 7.9f || !Alive, 1f);
            Assert.IsTrue(Alive, $"Giẫm AngryPig nhưng Player chết ({Now})");
            Assert.AreEqual(8f, (float)G.Get(enemyMove, "speed"), 0.01f, "Giẫm lần 1 nhưng AngryPig không tăng tốc lên 8");
            Assert.AreEqual(0f, (float)G.Get(enemyMove, "idleDuration"), 0.01f, "Giẫm lần 1 nhưng idleDuration không về 0");
            Assert.IsTrue(enemyCol.enabled, "AngryPig chết ngay lần giẫm đầu (thiết kế: 2 lần)");

            yield return WaitFor(() => (bool)G.Get(enemy, "isAngry"), 3f);
            Assert.IsTrue((bool)G.Get(enemy, "isAngry"), "Animation Hit1 không gọi SetAngryModOn → AngryPig không vào chế độ giận");
            Animator anim = (Animator)G.Get(enemy, "animator");
            Assert.IsTrue(anim.GetBool("AngryMod"), "Tham số Animator AngryMod không bật");
            Assert.AreEqual(3, G.Lives, "Giẫm AngryPig mà vẫn bị trừ mạng");
        }

        // R3: AngryPig đã nổi giận, giẫm lần 2 → chết, bị xóa, Player không mất mạng
        [UnityTest, Timeout(60000)]
        public IEnumerator TC_EN_05_AngryPig_GiamLan2_Chet()
        {
            yield return Arena();
            SpawnEnemy(AngryPigPrefab, ArenaX);

            Touch(enemyCol, 0f);
            yield return WaitFor(() => (bool)G.Get(enemy, "isAngry"), 3f);
            Assert.IsTrue((bool)G.Get(enemy, "isAngry"), "Lần giẫm 1 không làm AngryPig nổi giận");

            // Rời khỏi lưng Pig rồi giẫm lại
            yield return PlaceOnFloor(ArenaX - 8f);
            yield return FixedSteps(3);
            Touch(enemyCol, 0f);
            yield return WaitFor(() => !enemyCol.enabled || !Alive, 1f);
            Assert.IsTrue(Alive, $"Giẫm AngryPig lần 2 nhưng Player chết ({Now})");
            Assert.IsFalse(enemyCol.enabled, "Giẫm AngryPig lần 2 (đã giận) nhưng nó không chết");
            yield return WaitFor(() => enemyRoot == null, 2.5f);
            Assert.IsTrue(enemyRoot == null, "AngryPig chết nhưng không bị xóa sau 2.5s");
            Assert.AreEqual(3, G.Lives, "Giẫm AngryPig mà vẫn bị trừ mạng");
        }

        // R7: enemy đang phát animation chết (collider tắt, chưa bị xóa) → chạm ngang không gây hại
        [UnityTest, Timeout(60000)]
        public IEnumerator TC_EN_06_EnemyDangChet_ChamNgang_KhongGayHai()
        {
            yield return Arena();
            SpawnEnemy(MushroomPrefab, ArenaX);
            Vector2 side = ContactPivot(enemyCol, 90f);   // tính trước vì collider tắt thì bounds = 0

            Touch(enemyCol, 0f);
            yield return WaitFor(() => !enemyCol.enabled, 1f);
            Assert.IsFalse(enemyCol.enabled, "Giẫm Mushroom nhưng nó không chết");

            G.Teleport(player, side);
            yield return WaitFor(() => !Alive, 0.8f);
            Assert.IsTrue(Alive, $"Chạm Mushroom đang chết mà Player vẫn chết ({Now})");
            Assert.AreEqual(3, G.Lives, "Chạm enemy đang chết bị trừ mạng");
        }

        // R6 – kiểm tra vị trí đặt trên Level1..Level3:
        //  (a) mọi enemy phải có object cha – EnemyDamage.Die gọi Destroy(transform.parent.gameObject) (EnemyDamage.cs:47);
        //  (b) giẫm thật lên 1 enemy của mỗi loại (Mushroom, Trunk, Bee…) trong từng màn → enemy chết, bị xóa, không lỗi.
        // AngryPig cần 2 lần giẫm nên đã kiểm tra riêng ở TC_EN_04/05.
        [UnityTest, Timeout(300000)]
        public IEnumerator TC_EN_07_Level1Den3_GiamMoiLoaiEnemy_BiXoaKhongLoi()
        {
            LogAssert.ignoreFailingMessages = true;   // tự báo lỗi NRE trong thông báo Fail bên dưới
            var failures = new List<string>();
            var tried = new List<string>();

            foreach (int level in PlayableLevels)
            {
                yield return EnterLevel(level, lives: 50);
                DisableRangedEnemies();   // đạn của Trunk / Bee không được làm Player chết giữa chừng
                string scene = SceneName;

                var kinds = new Dictionary<string, Component>();
                foreach (Object o in G.FindAll("EnemyDamage"))
                {
                    Component c = (Component)o;
                    if (c.transform.parent == null)
                        failures.Add($"{scene}: enemy '{c.name}' không có object cha → EnemyDamage.cs:47 sẽ NullReferenceException khi chết");
                    if (c.GetType().Name != "EnemyDamage") continue;
                    string kind = Regex.Replace(c.name, @"\s*\(\d+\)$", "");
                    if (!kinds.ContainsKey(kind)) kinds[kind] = c;
                }

                foreach (KeyValuePair<string, Component> kv in kinds)
                {
                    yield return WaitFor(() => box.enabled, 6f);
                    if (!box.enabled) { failures.Add($"{scene}: Player không hồi sinh, bỏ qua {kv.Key}"); continue; }
                    Component e = kv.Value;
                    if (e == null) continue;

                    Collider2D ec = e.GetComponent<Collider2D>();
                    if (e.GetComponent(G.FindType("EnemyMovement")) is Behaviour mv) mv.enabled = false;
                    Rigidbody2D erb = e.GetComponent<Rigidbody2D>();
                    if (erb != null) { erb.bodyType = RigidbodyType2D.Kinematic; erb.linearVelocity = Vector2.zero; }
                    GameObject go = e.gameObject;
                    string name = go.name;
                    int err0 = UnexpectedErrors.Count;

                    Touch(ec, 0f);
                    yield return WaitFor(() => !ec.enabled || !Alive, 1f);
                    tried.Add($"{scene}:{kv.Key}");
                    if (ec.enabled)
                    {
                        failures.Add(Alive ? $"{scene}: giẫm '{name}' từ trên nhưng nó không chết"
                                           : $"{scene}: giẫm '{name}' từ trên nhưng Player lại chết");
                        continue;
                    }

                    yield return WaitFor(() => go == null, 3f);
                    List<string> now = UnexpectedErrors;
                    string newErr = string.Join(" | ", now.GetRange(err0, now.Count - err0));
                    if (go != null)
                        failures.Add($"{scene}: '{name}' chết nhưng không bị xóa sau 3s; lỗi: {(newErr == "" ? "không có" : newErr)}");
                    else if (newErr != "")
                        failures.Add($"{scene}: giẫm '{name}' sinh lỗi: {newErr}");
                }
            }

            Debug.Log("[TC_EN_07] Đã giẫm: " + string.Join(", ", tried));
            Assert.IsNotEmpty(tried, "Không có enemy thường nào trong Level1..Level3");
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        // Kiểm tra vị trí đặt: Trunk ở Level3 thấy Player trong tầm → dừng tuần tra, bắn, đạn làm Player chết
        [UnityTest, Timeout(60000)]
        public IEnumerator TC_EN_08_Level3_Trunk_PlayerTrongTam_DungLaiVaBan()
        {
            yield return EnterLevel(Level3, lives: 5);
            Component ranged = FindTrunk();
            Assert.IsNotNull(ranged, "Level3 không có Trunk (RangedEnemy bắn ngang)");
            DisableRangedEnemies(keep: ranged);   // chỉ để Trunk này bắn
            Behaviour move = (Behaviour)G.Get(ranged, "enemyMovement");
            Assert.IsNotNull(move, "RangedEnemy không tìm thấy EnemyMovement");
            BoxCollider2D tc = (BoxCollider2D)G.Get(ranged, "boxCollider");
            float range = (float)G.Get(ranged, "range");
            float cd = (float)G.Get(ranged, "colliderDistance");
            GameObject[] bullets = (GameObject[])G.Get(ranged, "bullets");
            int activeBefore = System.Array.FindAll(bullets, b => b != null && b.activeInHierarchy).Length;

            // Vùng nhìn giống RangedEnemy.PlayerInSight: tâm lệch range * scale.x * colliderDistance theo transform.right
            Transform t = ranged.transform;
            Vector3 sight = tc.bounds.center + t.right * range * t.localScale.x * cd;
            float dir = Mathf.Sign(sight.x - tc.bounds.center.x);
            TeleportCenterTo(new Vector2(tc.bounds.center.x + dir * (tc.bounds.extents.x + box.bounds.extents.x + 0.8f),
                                         tc.bounds.center.y));

            yield return WaitFor(() => !move.enabled, 1.5f);
            Assert.IsFalse(move.enabled, $"Player đứng trong tầm nhìn của Trunk nhưng Trunk vẫn tuần tra ({Now})");

            yield return WaitFor(() => !Alive ||
                System.Array.FindAll(bullets, b => b != null && b.activeInHierarchy).Length > activeBefore, 4f);
            yield return WaitDied("Đứng trước Trunk", 4f);
            yield return WaitRespawn();
            Assert.AreEqual(4, G.Lives, "Trúng đạn Trunk: số mạng không giảm đúng 1 (5 → 4)");
        }
    }
}
