using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DungeonEscape.Tests
{
    /// <summary>
    /// Kiểm thử bẫy / vật nguy hiểm: TC-HZ-01 .. TC-HZ-07 (P1-32).
    ///
    /// Phân vùng tương đương theo cơ chế gây chết:
    ///   H1 SpikeTrap (va chạm, không trigger) · H2 Trap/SawTrap (va chạm + trigger) · H3 FireTrap (trigger, luôn bật)
    ///   H4 EnemyProjectile (đạn) · H5 DeathZone · lớp không hợp lệ: vật không phải Player chạm bẫy.
    /// Không lặp lại TC_TR_01 (Level1, chạm tâm Spike/Saw/DeathZone).
    /// </summary>
    public class HazardTests : ECTestBase
    {
        // H1 + hướng chạm: Spikes làm chết khi chạm từ trên xuống và cả khi chạm từ cạnh, mỗi lần trừ đúng 1 mạng
        [UnityTest, Timeout(60000)]
        public IEnumerator TC_HZ_01_Spikes_ChamTuTrenVaTuCanh_ChetTru1Mang()
        {
            yield return EnterLevel(Level2, lives: 5);
            Floor(ArenaX, ArenaY);
            GameObject spike = Spawn(SpikesPrefab, new Vector2(ArenaX + 4f, ArenaY));
            PutOnFloor(spike, ArenaX + 4f, ArenaY);
            Collider2D sc = spike.GetComponent<Collider2D>();

            // 1) Rơi từ trên xuống gai
            yield return PlaceOnFloor(ArenaX - 4f);
            G.Teleport(player, ContactPivot(sc, 0f));
            yield return WaitDied("Chạm Spikes từ trên xuống");
            yield return WaitRespawn();
            Assert.AreEqual(4, G.Lives, "Chạm Spikes từ trên: số mạng không giảm đúng 1 (5 → 4)");
            Assert.AreEqual("4", HudLives, "HUD không cập nhật số mạng sau khi chạm Spikes");

            // 2) Chạm cạnh gai khi đang đứng trên sàn: đẩy gai sát vào cạnh phải của Player
            yield return PlaceOnFloor(ArenaX);
            float x = box.bounds.max.x + sc.bounds.extents.x - 0.06f;
            PutOnFloor(spike, x, ArenaY);
            yield return WaitDied("Chạm cạnh Spikes");
            yield return WaitRespawn();
            Assert.AreEqual(3, G.Lives, "Chạm cạnh Spikes: số mạng không giảm đúng 1 (4 → 3)");
            Assert.AreEqual("3", HudLives, "HUD không cập nhật số mạng");
        }

        // H3: Fire là trigger luôn bật (bộ hẹn giờ trong FireTrap.cs đã bị comment) → chạm lần nào cũng chết
        [UnityTest, Timeout(60000)]
        public IEnumerator TC_HZ_02_Fire_ChamVao_ChetTru1Mang_LanSauVanGayChet()
        {
            yield return EnterLevel(Level2, lives: 5);
            Floor(ArenaX, ArenaY);
            GameObject fire = Spawn(FirePrefab, new Vector2(ArenaX + 4f, ArenaY));
            PutOnFloor(fire, ArenaX + 4f, ArenaY);
            Collider2D fc = fire.GetComponent<Collider2D>();
            Assert.IsTrue(fc.isTrigger, "Collider của Fire không phải trigger như thiết kế");

            for (int i = 1; i <= 2; i++)
            {
                yield return PlaceOnFloor(ArenaX);
                TeleportCenterTo(new Vector2(fc.bounds.center.x, box.bounds.center.y));
                yield return WaitDied($"Chạm Fire lần {i}");
                yield return WaitRespawn();
                Assert.AreEqual(5 - i, G.Lives, $"Chạm Fire lần {i}: số mạng không giảm đúng 1");
                Assert.AreEqual($"{5 - i}", HudLives, $"HUD không cập nhật số mạng sau lần chạm Fire thứ {i}");
            }
        }

        // Giá trị biên kích thước: Player rộng 0.94 > 1 gai 0.79 → rơi vào khe giữa 2 gai sẽ chạm cả 2 trong cùng 1 bước vật lý.
        // PlayerLife.Die() không chặn khi đã chết (PlayerLife.cs:60-78) → nghi mất 2 mạng.
        [UnityTest, Timeout(60000)]
        public IEnumerator TC_HZ_03_RoiXuongKheGiua2Gai_ChiMat1Mang()
        {
            yield return EnterLevel(Level2, lives: 5);
            Floor(ArenaX, ArenaY);
            GameObject s1 = Spawn(SpikesPrefab, new Vector2(ArenaX + 4f, ArenaY));
            PutOnFloor(s1, ArenaX + 4f, ArenaY);
            Collider2D c1 = s1.GetComponent<Collider2D>();
            GameObject s2 = Spawn(SpikesPrefab, new Vector2(ArenaX + 6f, ArenaY));
            PutOnFloor(s2, c1.bounds.max.x + c1.bounds.extents.x, ArenaY);
            float seam = c1.bounds.max.x;

            yield return PlaceOnFloor(ArenaX - 4f);
            // Tâm Player ngay trên khe, đáy cao hơn đỉnh gai 0.2 → rơi xuống chạm cả 2 gai cùng lúc
            float top = c1.bounds.max.y;
            TeleportCenterTo(new Vector2(seam, top + box.bounds.extents.y + 0.2f));
            yield return WaitDied("Rơi xuống 2 gai");
            yield return WaitRespawn();
            yield return WaitFor(() => false, 0.5f);

            Assert.AreEqual(4, G.Lives,
                $"Chạm 2 gai cùng lúc bị trừ {5 - G.Lives} mạng thay vì 1 (PlayerLife.Die không chặn khi đã chết). HUD={HudLives}");
            Assert.AreEqual("4", HudLives, "HUD mạng không khớp");
        }

        // H4: đạn của Trunk (EnemyProjectile) trúng Player → Player chết, trừ 1 mạng, đạn nổ (tắt collider) rồi tắt hẳn
        [UnityTest, Timeout(60000)]
        public IEnumerator TC_HZ_04_DanTrunk_TrungPlayer_ChetVaDanNo()
        {
            yield return EnterLevel(Level3, lives: 5);
            Component ranged = FindTrunk();
            Assert.IsNotNull(ranged, "Level3 không có Trunk (RangedEnemy bắn ngang)");
            // Dừng mọi enemy bắn đạn (Trunk, Bee) để không lấy viên đạn đang thử / không bắn trúng Player giữa chừng
            DisableRangedEnemies();

            GameObject[] bullets = (GameObject[])G.Get(ranged, "bullets");
            Assert.IsNotEmpty(bullets, "Trunk ở Level3 không có viên đạn nào");
            GameObject bullet = System.Array.Find(bullets, b => b != null && !b.activeInHierarchy) ?? bullets[0];
            Component proj = G.Comp(bullet, "EnemyProjectile");

            yield return WaitFor(() => Mathf.Abs(rb.linearVelocity.y) < 0.05f, 2f);
            bullet.transform.position = box.bounds.center;
            G.Call(proj, "ActivateProjectile");
            Physics2D.SyncTransforms();

            yield return WaitDied("Đạn Trunk trúng Player");
            Assert.IsTrue((bool)G.Get(proj, "hit"), "Đạn trúng Player nhưng không chuyển sang trạng thái nổ (hit)");
            Assert.IsFalse(((Collider2D)G.Get(proj, "coll")).enabled, "Đạn đã nổ nhưng collider vẫn bật");

            yield return WaitRespawn();
            Assert.AreEqual(4, G.Lives, "Trúng đạn: số mạng không giảm đúng 1 (5 → 4)");
            Assert.AreEqual("4", HudLives, "HUD không cập nhật số mạng");
            yield return WaitFor(() => !bullet.activeInHierarchy, 2f);
            Assert.IsFalse(bullet.activeInHierarchy, "Đạn nổ xong nhưng không tắt (animation Explode không gọi Deactivate)");
        }

        // Lớp không hợp lệ: vật không phải Player (enemy) chạm Spikes / Fire → không có gì xảy ra, không lỗi
        [UnityTest, Timeout(60000)]
        public IEnumerator TC_HZ_05_EnemyChamBay_KhongAnhHuong_KhongLoi()
        {
            yield return EnterLevel(Level2, lives: 3);
            Floor(ArenaX, ArenaY);
            yield return PlaceOnFloor(ArenaX - 10f);

            GameObject spike = Spawn(SpikesPrefab, new Vector2(ArenaX + 4f, ArenaY));
            PutOnFloor(spike, ArenaX + 4f, ArenaY);
            GameObject fire = Spawn(FirePrefab, new Vector2(ArenaX + 8f, ArenaY));
            PutOnFloor(fire, ArenaX + 8f, ArenaY);

            GameObject root = Spawn(MushroomPrefab, new Vector2(ArenaX + 4f, ArenaY + 2f));
            Component enemy = root.GetComponentInChildren(G.FindType("EnemyDamage"));
            ((Behaviour)enemy.GetComponent(G.FindType("EnemyMovement"))).enabled = false;
            Collider2D ec = enemy.GetComponent<Collider2D>();

            // Thả Mushroom rơi xuống gai
            enemy.transform.position = new Vector3(ArenaX + 4f, ArenaY + 2f, 0f);
            Physics2D.SyncTransforms();
            yield return WaitFor(() => false, 1f);
            // Đưa Mushroom vào giữa Fire
            enemy.transform.position = new Vector3(ArenaX + 8f, ArenaY + ec.bounds.extents.y + 0.05f, 0f);
            Physics2D.SyncTransforms();
            yield return WaitFor(() => false, 1f);

            Assert.IsTrue(enemy != null && ec.enabled, "Enemy chạm bẫy bị chết / mất collider");
            Assert.IsTrue(Alive, $"Enemy chạm bẫy nhưng Player ở xa lại chết ({Now})");
            Assert.AreEqual(3, G.Lives, "Enemy chạm bẫy làm Player mất mạng");
            Assert.IsEmpty(UnexpectedErrors, $"Có lỗi khi vật không phải Player chạm bẫy: {Errors}");
        }

        // Kiểm tra vị trí đặt: mỗi loại bẫy có trong Level1..Level3 phải làm Player chết khi chạm
        // (mở rộng TC_TR_01 – vốn chỉ thử Spike/Saw/DeathZone – sang mọi loại bẫy của các màn)
        [UnityTest, Timeout(300000)]
        public IEnumerator TC_HZ_06_Level1Den3_MoiLoaiBay_LamChetPlayer()
        {
            var failures = new List<string>();
            var report = new List<string>();

            foreach (int level in PlayableLevels)
            {
                yield return EnterLevel(level, lives: 50);
                string scene = SceneName;

                foreach (string type in HazardTypes)
                {
                    Component hazard = FirstExact(type);
                    if (hazard == null) continue;

                    yield return WaitFor(() => box.enabled, 6f);
                    int before = G.Lives;
                    Collider2D col = hazard.GetComponent<Collider2D>();
                    TeleportCenterTo(col.bounds.center);   // tính ngay lúc dịch chuyển vì Saw / Pendulum đang chuyển động
                    yield return WaitFor(() => !box.enabled, 2f);

                    if (box.enabled)
                    {
                        failures.Add($"{scene}: chạm {type} '{hazard.name}' tại {col.bounds.center} nhưng Player không chết");
                        continue;
                    }
                    yield return WaitRespawn();
                    if (G.Lives >= before)
                        failures.Add($"{scene}: chạm {type} '{hazard.name}' nhưng số mạng không giảm ({before} → {G.Lives})");
                    report.Add($"{scene}:{type}");
                }
            }

            Debug.Log("[TC_HZ_06] Đã thử: " + string.Join(", ", report));
            Assert.IsNotEmpty(report, "Không tìm thấy bẫy nào trong Level1..Level3");
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        // Kiểm tra cấu hình: object tag Trap phải có script gây chết; mọi script gây chết phải có collider bật,
        // nằm trên layer có va chạm với Player
        [UnityTest, Timeout(180000)]
        public IEnumerator TC_HZ_07_Level1Den3_MoiVatTagTrap_CoScriptVaCollider()
        {
            var failures = new List<string>();
            int checkedCount = 0;

            foreach (int level in PlayableLevels)
            {
                yield return LoadOnly(level);
                string scene = SceneName;
                int playerLayer = G.Player().layer;

                foreach (GameObject go in GameObject.FindGameObjectsWithTag("Trap"))
                {
                    // Script gây chết có thể nằm ở chính object / object con, hoặc ở object cha (vd. Level2 'SpikesTrail':
                    // 1 collider dài + SpikeTrap ở cha, các gai con chỉ để trang trí). Khi đó collider của cha phải phủ hình gai.
                    bool hasHazard = false;
                    foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>())
                        if (IsHazardScript(mb)) hasHazard = true;
                    if (!hasHazard)
                    {
                        for (Transform t = go.transform.parent; t != null && !hasHazard; t = t.parent)
                            foreach (MonoBehaviour mb in t.GetComponents<MonoBehaviour>())
                            {
                                if (!IsHazardScript(mb)) continue;
                                hasHazard = true;
                                Collider2D pc = mb.GetComponent<Collider2D>();
                                Renderer r = go.GetComponent<Renderer>();
                                if (pc != null && r != null && !pc.bounds.Intersects(r.bounds))
                                    failures.Add($"{scene}: '{Path(go)}' nằm ngoài collider của '{Path(t.gameObject)}' → nhìn thấy gai nhưng chạm không chết");
                            }
                    }
                    if (!hasHazard) failures.Add($"{scene}: '{Path(go)}' có tag Trap nhưng không có script gây chết (ở chính nó, object con hay object cha)");
                    checkedCount++;
                }

                foreach (string type in HazardTypes)
                foreach (Object o in G.FindAll(type))
                {
                    Component h = (Component)o;
                    if (h.GetType().Name != type) continue;
                    Collider2D c = h.GetComponent<Collider2D>();
                    if (c == null || !c.enabled)
                        failures.Add($"{scene}: {type} '{Path(h.gameObject)}' không có Collider2D đang bật");
                    else if (Physics2D.GetIgnoreLayerCollision(h.gameObject.layer, playerLayer))
                        failures.Add($"{scene}: {type} '{Path(h.gameObject)}' nằm trên layer không va chạm với Player");
                    checkedCount++;
                }
            }

            Assert.Greater(checkedCount, 0, "Không có bẫy nào để kiểm tra");
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        // ================= Hàm phụ =================

        /// <summary>Lấy component đầu tiên đúng tên lớp (FindAll("Trap") trả cả SawTrap/FireTrap là lớp con).</summary>
        private static Component FirstExact(string type)
        {
            foreach (Object o in G.FindAll(type))
            {
                Component c = (Component)o;
                Collider2D col = c.GetComponent<Collider2D>();
                if (c.GetType().Name == type && col != null && col.enabled) return c;
            }
            return null;
        }

        private static bool IsHazardScript(MonoBehaviour mb) =>
            mb != null && System.Array.IndexOf(HazardTypes, mb.GetType().Name) >= 0;

        private static string Path(GameObject go)
        {
            string p = go.name;
            for (Transform t = go.transform.parent; t != null; t = t.parent) p = t.name + "/" + p;
            return p;
        }
    }
}
