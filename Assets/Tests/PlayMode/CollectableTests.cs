using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DungeonEscape.Tests
{
    /// <summary>
    /// Kiểm thử vật phẩm: TC-CL-01 .. TC-CL-08 (P1-32).
    ///   Cherry (tag Cherry) → ItemCollector: Foods +1, lưu PlayerPrefs "Foods", HUD cherriesText
    ///   Heart  (tag Buff)   → PlayerLife: LifeCount +1, HUD heartsCount (giới hạn maxLifeBound = 100)
    ///   Key    (tag Key)    → ItemCollector: keys +1 (chỉ trong bộ nhớ), HUD keysText; Finish cần đủ 3 Key
    /// Không lặp lại TC_IT_01 (Cherry từ 0, giữ khi vào lại màn), TC_IT_02 (Key khi hồi sinh / vào lại),
    /// TC_LV_01/02 (Level1, 2/3 Key).
    /// </summary>
    public class CollectableTests : ECTestBase
    {
        private const string FinishLockedMsg = "Collect all keys to activate this portal!";

        private IEnumerator Arena(int lives = 3, int foods = 0)
        {
            yield return EnterLevel(Level2, lives: lives, foods: foods);
            Floor(ArenaX, ArenaY);
            yield return PlaceOnFloor(ArenaX - 8f);
        }

        /// <summary>Đặt vật phẩm cách Player dx đơn vị theo phương ngang, ngang tầm tâm Player.</summary>
        private GameObject SpawnItem(string prefab, float dx) =>
            Spawn(prefab, new Vector2(box.bounds.center.x + dx, box.bounds.center.y));

        /// <summary>Dịch chuyển Player trùng tâm vật phẩm rồi chờ điều kiện.</summary>
        private IEnumerator Touch(Vector2 itemCenter, System.Func<bool> done)
        {
            TeleportCenterTo(itemCenter);
            yield return WaitFor(done, 1.5f);
        }

        // Giá trị biên: Foods đã có sẵn 5 (khác 0) → nhặt 1 Cherry thành 6, lưu ngay, HUD đúng
        [UnityTest, Timeout(60000)]
        public IEnumerator TC_CL_01_Cherry_FoodsCoSan5_Nhat1_Thanh6_HUD()
        {
            yield return Arena(foods: 5);
            Assert.AreEqual("5", HudFoods, "Vào màn với Foods = 5 nhưng HUD không hiện 5");

            GameObject cherry = SpawnItem(CherryPrefab, 3f);
            Vector2 c = cherry.GetComponent<Collider2D>().bounds.center;
            yield return Touch(c, () => cherry == null);

            Assert.IsTrue(cherry == null, "Chạm Cherry nhưng Cherry không biến mất");
            Assert.AreEqual(6, PlayerPrefs.GetInt("Foods"), "Foods không tăng từ 5 lên 6 (cộng dồn từ giá trị đã lưu)");
            Assert.AreEqual("6", HudFoods, "HUD không cập nhật số Cherry");
        }

        // Nhặt 4 Cherry liên tiếp → Foods = 4; đi qua lại chỗ cũ không cộng thêm
        [UnityTest, Timeout(60000)]
        public IEnumerator TC_CL_02_Nhat4Cherry_MoiQuaChiTinh1Lan()
        {
            yield return Arena();
            var spots = new List<Vector2>();
            var items = new List<GameObject>();
            for (int i = 0; i < 4; i++)
            {
                GameObject ch = SpawnItem(CherryPrefab, 3f + 2.5f * i);
                items.Add(ch);
                spots.Add(ch.GetComponent<Collider2D>().bounds.center);
            }

            for (int i = 0; i < 4; i++)
            {
                int expect = i + 1;
                yield return Touch(spots[i], () => PlayerPrefs.GetInt("Foods") >= expect);
                Assert.AreEqual(expect, PlayerPrefs.GetInt("Foods"), $"Nhặt Cherry thứ {expect}: Foods sai");
                Assert.AreEqual($"{expect}", HudFoods, $"Nhặt Cherry thứ {expect}: HUD sai");
            }

            // Quay lại các vị trí đã nhặt
            foreach (Vector2 s in spots) yield return Touch(s, () => false);
            Assert.AreEqual(4, PlayerPrefs.GetInt("Foods"), "Đi qua chỗ Cherry đã nhặt mà Foods vẫn tăng");
            Assert.IsTrue(items.TrueForAll(x => x == null), "Có Cherry đã nhặt nhưng không bị xóa");
        }

        // Nhặt Cherry rồi chết → hồi sinh vẫn giữ Foods (đã lưu PlayerPrefs) và HUD
        [UnityTest, Timeout(60000)]
        public IEnumerator TC_CL_03_NhatCherryRoiChet_FoodsVanGiu()
        {
            yield return Arena();
            GameObject cherry = SpawnItem(CherryPrefab, 3f);
            yield return Touch(cherry.GetComponent<Collider2D>().bounds.center, () => PlayerPrefs.GetInt("Foods") == 1);
            Assert.AreEqual(1, PlayerPrefs.GetInt("Foods"), "Nhặt Cherry nhưng Foods không tăng");

            yield return G.KillPlayer(player);
            Assert.IsTrue(Alive, "Còn mạng nhưng Player không hồi sinh");
            Assert.AreEqual(1, PlayerPrefs.GetInt("Foods"), "Chết rồi hồi sinh thì mất Foods đã nhặt");
            Assert.AreEqual("1", HudFoods, "Hồi sinh xong HUD Cherry bị đặt lại");
        }

        // Heart (+1 mạng): HUD và LifeCount tăng; giá trị phải được lưu vào PlayerPrefs như khi mất mạng
        [UnityTest, Timeout(60000)]
        public IEnumerator TC_CL_04_Heart_TangMang_HUDVaPlayerPrefs()
        {
            yield return Arena(lives: 3);
            GameObject heart = SpawnItem(HeartPrefab, 3f);
            yield return Touch(heart.GetComponent<Collider2D>().bounds.center, () => heart == null);

            Assert.IsTrue(heart == null, "Chạm Heart nhưng Heart không biến mất");
            Assert.AreEqual(4, LifeField, "Nhặt Heart nhưng số mạng không tăng từ 3 lên 4");
            Assert.AreEqual("4", HudLives, "HUD không cập nhật số mạng sau khi nhặt Heart");
            Assert.AreEqual(4, G.Lives,
                $"Nhặt Heart: PlayerPrefs LifeCount vẫn là {G.Lives} (PlayerLife.cs:95 chỉ tăng biến, không lưu; " +
                "thoát màn trước khi chết là mất mạng vừa nhặt)");
        }

        // Giá trị biên maxLifeBound = 100: 99 → 100 được; đang 100 nhặt thêm phải giữ 100
        [UnityTest, Timeout(60000)]
        public IEnumerator TC_CL_05_Heart_O100Mang_KhongVuotMax()
        {
            yield return Arena(lives: 99);
            int max = (int)G.Get(life, "maxLifeBound");
            Assert.AreEqual(100, max, "maxLifeBound của nhân vật không phải 100 như CharacterData");

            GameObject h1 = SpawnItem(HeartPrefab, 3f);
            yield return Touch(h1.GetComponent<Collider2D>().bounds.center, () => h1 == null);
            Assert.AreEqual(100, LifeField, "99 mạng nhặt Heart không lên 100");

            yield return PlaceOnFloor(ArenaX - 8f);
            GameObject h2 = SpawnItem(HeartPrefab, 3f);
            yield return Touch(h2.GetComponent<Collider2D>().bounds.center, () => h2 == null);
            Assert.AreEqual(100, LifeField,
                $"Đang 100 mạng (max) nhặt Heart thành {LifeField} – vượt maxLifeBound (PlayerLife.cs:95 không kiểm tra giới hạn)");
            Assert.AreEqual("100", HudLives, "HUD hiện số mạng vượt giới hạn");
        }

        // Key: HUD đếm 0 → 1 → 2 → 3; Finish khi 0 Key hiện thông báo mới và không qua màn; đủ 3 Key thì qua màn.
        // Lỗi game ở Finish (hudMenu chưa gán → NRE) được ghi nhận "mềm" để test vẫn kiểm tra hết các bước còn lại.
        [UnityTest, Timeout(90000)]
        public IEnumerator TC_CL_06_Key_HUDTangDan_FinishKhoaKhiThieuKey()
        {
            yield return Arena();
            LogAssert.ignoreFailingMessages = true;   // NRE của Finish được ghi vào danh sách lỗi bên dưới
            var bugs = new List<string>();
            Assert.AreEqual("0", HudKeys, "Vào màn nhưng HUD Key không phải 0");
            Component finish = G.Find("Finish");
            Component hud = G.Find("HUDMenu");
            Collider2D fc = finish.GetComponent<Collider2D>();
            bool hudWired = (Object)G.Get(finish, "hudMenu") != null;
            if (!hudWired) bugs.Add("Finish.hudMenu chưa được gán trong Level2");

            // 0 Key → Finish khóa
            int e0 = errors.Count;
            TeleportCenterTo(fc.bounds.center);
            yield return WaitFor(() => G.TextOf(G.Get(hud, "messageText")) != "" || errors.Count > e0, 2f);
            string msg = G.TextOf(G.Get(hud, "messageText"));
            if (msg != FinishLockedMsg)
                bugs.Add($"0 Key chạm Finish: không hiện '{FinishLockedMsg}' (HUD = '{msg}'); lỗi: " +
                         string.Join(" | ", errors.GetRange(e0, errors.Count - e0)));
            yield return WaitFor(() => false, 1.5f);
            Assert.AreEqual(Level2, G.ActiveIndex, "Chưa có Key nào mà đã qua màn");

            // Nhặt 3 Key ở sân thử
            yield return PlaceOnFloor(ArenaX - 8f);
            var spots = new List<Vector2>();
            for (int i = 0; i < 3; i++)
                spots.Add(SpawnItem(KeyPrefab, 3f + 2.5f * i).GetComponent<Collider2D>().bounds.center);
            for (int i = 0; i < 3; i++)
            {
                int expect = i + 1;
                yield return Touch(spots[i], () => Keys >= expect);
                Assert.AreEqual(expect, Keys, $"Nhặt Key thứ {expect}: số Key sai");
                Assert.AreEqual($"{expect}", HudKeys, $"Nhặt Key thứ {expect}: HUD sai");
            }

            // Đủ 3 Key → qua màn (Level2 → build index kế tiếp)
            int e1 = errors.Count;
            TeleportCenterTo(fc.bounds.center);
            yield return WaitFor(() => G.ActiveIndex != Level2, 8f);
            if (G.ActiveIndex == Level2)
            {
                bugs.Add("Đủ 3 Key chạm Finish nhưng KHÔNG qua màn; lỗi: " +
                         string.Join(" | ", errors.GetRange(e1, errors.Count - e1)));
            }
            else
            {
                if (PlayerPrefs.GetInt("Level2") != 2) bugs.Add("Level2 chưa được đánh dấu Completed (2)");
                if (PlayerPrefs.GetInt("Level3") != 1) bugs.Add("Level3 chưa được mở (Unlocked = 1)");
                if (G.ActiveIndex != Level3) bugs.Add($"Qua Level2 nhưng vào scene '{SceneName}' (index {G.ActiveIndex}) thay vì Level3");
            }
            Assert.IsEmpty(bugs, string.Join("\n", bugs));
        }

        // Kiểm tra cấu hình HUD theo màn: số Key / Cherry trên HUD và thông báo của Finish phải được gán
        [UnityTest, Timeout(180000)]
        public IEnumerator TC_CL_09_Level1Den3_HUDKeyCherryVaThongBaoFinish_DaGan()
        {
            var failures = new List<string>();
            foreach (int level in PlayableLevels)
            {
                yield return LoadOnly(level);
                Component col = G.Comp(G.Player(), "ItemCollector");
                if ((Object)G.Get(col, "keysText") == null)
                    failures.Add($"{SceneName}: ItemCollector.keysText chưa gán → HUD không hiện số Key, " +
                                 "NullReferenceException ở ItemCollector.cs:45 mỗi lần vào màn và mỗi lần nhặt Key");
                if ((Object)G.Get(col, "cherriesText") == null)
                    failures.Add($"{SceneName}: ItemCollector.cherriesText chưa gán → HUD không hiện số Cherry");
                Component fin = G.TryFind("Finish");
                if (fin != null && (Object)G.Get(fin, "hudMenu") == null)
                    failures.Add($"{SceneName}: Finish.hudMenu chưa gán → chạm Finish ném NullReferenceException " +
                                 "(Finish.cs:32 khi thiếu Key, Finish.cs:37 khi đủ Key – không qua được màn)");
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        // Kiểm tra vị trí đặt: Finish.cs:30 yêu cầu 3 Key → mỗi màn phải có ít nhất 3 Key
        [UnityTest, Timeout(180000)]
        public IEnumerator TC_CL_07_Level1Den3_MoiManCoDu3Key()
        {
            var failures = new List<string>();
            foreach (int level in PlayableLevels)
            {
                yield return LoadOnly(level);
                int keys = GameObject.FindGameObjectsWithTag("Key").Length;
                if (G.TryFind("Finish") == null) failures.Add($"{SceneName}: không có Finish");
                if (keys < 3) failures.Add($"{SceneName}: chỉ có {keys} Key, Finish cần 3 → không thể qua màn");
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        // Kiểm tra vị trí đặt: Cherry / Key / Heart không được nằm chồng lên bẫy đứng yên (nhặt là chết);
        // chồng lên bẫy di chuyển chỉ ghi cảnh báo
        [UnityTest, Timeout(180000)]
        public IEnumerator TC_CL_08_Level1Den3_VatPhamKhongNamTrongBay()
        {
            var failures = new List<string>();
            var warnings = new List<string>();
            int count = 0;
            foreach (int level in PlayableLevels)
            {
                yield return LoadOnly(level);
                yield return new WaitForFixedUpdate();
                foreach (string tag in new[] { "Cherry", "Key", "Buff" })
                foreach (GameObject item in GameObject.FindGameObjectsWithTag(tag))
                {
                    Collider2D ic = item.GetComponent<Collider2D>();
                    if (ic == null) { failures.Add($"{SceneName}: {tag} '{item.name}' không có collider"); continue; }
                    count++;
                    foreach (Collider2D hit in Physics2D.OverlapBoxAll(ic.bounds.center, ic.bounds.size, 0f))
                    {
                        if (hit == ic || !IsHazard(hit)) continue;
                        string msg = $"{SceneName}: {tag} '{item.name}' tại {(Vector2)ic.bounds.center} chồng lên bẫy '{hit.name}'";
                        // Bẫy di chuyển (Saw theo waypoint, quả cầu của Pendulum) chỉ đi ngang qua vật phẩm theo chu kỳ –
                        // có thể là thiết kế "canh thời điểm" nên chỉ ghi cảnh báo, không tính là lỗi.
                        if (IsMovingHazard(hit)) warnings.Add(msg + " (bẫy di chuyển – cần dev xác nhận thiết kế)");
                        else failures.Add(msg);
                    }
                }
            }
            Debug.Log($"[TC_CL_08] Đã kiểm tra {count} vật phẩm");
            if (warnings.Count > 0) Debug.LogWarning("[TC_CL_08] Cảnh báo: " + string.Join(" | ", warnings));
            Assert.Greater(count, 0, "Không có vật phẩm nào trong Level1..Level3");
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        private static bool IsMovingHazard(Collider2D c)
        {
            foreach (MonoBehaviour mb in c.GetComponentsInParent<MonoBehaviour>())
                if (mb != null && (mb.GetType().Name == "WaypointFollower" || mb.GetType().Name == "Pendulum")) return true;
            return false;
        }

        private static bool IsHazard(Collider2D c)
        {
            if (c.CompareTag("Trap")) return true;
            foreach (MonoBehaviour mb in c.GetComponents<MonoBehaviour>())
                if (mb != null && System.Array.IndexOf(HazardTypes, mb.GetType().Name) >= 0) return true;
            return false;
        }
    }
}
