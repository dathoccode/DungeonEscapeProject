using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DungeonEscape.Tests
{
    /// <summary>Key, Finish, Cherry: TC-LV-01, TC-LV-02, TC-IT-01, TC-IT-02.</summary>
    public class LevelFlowTests : GameTestBase
    {
        private GameObject player;
        private Component collector;

        private IEnumerator EnterLevel1()
        {
            G.FreshProgress(lives: 20);   // nhiều mạng để lỡ chạm bẫy khi dịch chuyển vẫn chạy tiếp được
            yield return G.LoadScene(G.Level1);
            player = G.Player();
            collector = G.Comp(player, "ItemCollector");
            yield return new WaitForSeconds(1f);
        }

        private int Keys => (int)G.Call(collector, "GetKeys");

        /// <summary>Dịch chuyển tới vật phẩm, chờ nhặt xong; nếu lỡ chết thì chờ hồi sinh.</summary>
        private IEnumerator Collect(GameObject item, System.Func<bool> collected)
        {
            G.Teleport(player, item.transform.position);
            yield return G.WaitUntil(collected, 2f);
            yield return G.WaitUntil(() => G.ColliderOn(player), 6f);
        }

        private IEnumerator CollectKeys(int count)
        {
            GameObject[] keys = GameObject.FindGameObjectsWithTag("Key");
            Assert.GreaterOrEqual(keys.Length, count, $"Level1 chỉ có {keys.Length} Key");
            for (int i = 0; i < count; i++)
            {
                int target = i + 1;
                yield return Collect(keys[i], () => Keys >= target);
            }
            Assert.AreEqual(count, Keys, "Số Key nhặt được không đúng");
        }

        // Biên dưới: 2/3 Key → không qua màn
        [UnityTest]
        public IEnumerator TC_LV_01_ThieuKey_KhongQuaMan_HienThongBao()
        {
            yield return EnterLevel1();
            yield return CollectKeys(2);

            Component hud = G.Find("HUDMenu");
            G.Teleport(player, G.Find("Finish").transform.position);
            yield return G.WaitUntil(() => G.TextOf(G.Get(hud, "messageText")).Contains("collect all 3 keys"), 2f);
            StringAssert.Contains("collect all 3 keys", G.TextOf(G.Get(hud, "messageText")), "Thiếu Key nhưng không hiện thông báo");

            yield return new WaitForSeconds(3f);
            Assert.AreEqual(G.Level1, G.ActiveIndex, "Mới có 2/3 Key mà đã qua màn");
        }

        // Đúng ngưỡng: 3/3 Key → qua màn, cập nhật trạng thái level (bảng chuyển trạng thái 4.5)
        [UnityTest]
        public IEnumerator TC_LV_02_DuKey_QuaMan_Level1CompletedLevel2Unlocked()
        {
            yield return EnterLevel1();
            yield return CollectKeys(3);

            G.Teleport(player, G.Find("Finish").transform.position);
            yield return G.WaitUntil(() => G.ActiveIndex == G.Level2, 8f);

            Assert.AreEqual(G.Level2, G.ActiveIndex, "Đủ 3 Key chạm Finish nhưng không chuyển sang Level2");
            Assert.AreEqual(2, PlayerPrefs.GetInt("Level1"), "Level1 chưa được đánh dấu Completed (2)");
            Assert.AreEqual(1, PlayerPrefs.GetInt("Level2"), "Level2 chưa được mở (Unlocked = 1)");
            Assert.AreEqual(0, PlayerPrefs.GetInt("LatestCheckpointIndex"), "Checkpoint không được xóa khi qua màn");
        }

        [UnityTest]
        public IEnumerator TC_IT_01_NhatCherry_FoodsTangVaDuocLuu()
        {
            yield return EnterLevel1();
            GameObject[] cherries = GameObject.FindGameObjectsWithTag("Cherry");
            Assert.Greater(cherries.Length, 0, "Level1 không có Cherry");

            yield return Collect(cherries[0], () => PlayerPrefs.GetInt("Foods") == 1);
            Assert.AreEqual(1, PlayerPrefs.GetInt("Foods"), "Nhặt Cherry nhưng Foods không tăng");
            Assert.AreEqual("1", G.TextOf(G.Get(collector, "cherriesText")), "HUD không cập nhật số Cherry");

            // Chơi lại level: Foods vẫn giữ
            yield return G.LoadScene(G.Level1);
            Component again = G.Comp(G.Player(), "ItemCollector");
            Assert.AreEqual("1", G.TextOf(G.Get(again, "cherriesText")), "Vào lại level thì mất số Foods đã nhặt");
        }

        [UnityTest]
        public IEnumerator TC_IT_02_Key_GiuKhiHoiSinh_VeKhongKhiVaoLaiLevel()
        {
            yield return EnterLevel1();
            yield return CollectKeys(1);
            Assert.AreEqual("1", G.TextOf(G.Get(collector, "keysText")), "HUD không cập nhật số Key");

            yield return G.KillPlayer(player);
            Assert.AreEqual(1, Keys, "Chết và hồi sinh làm mất Key đã nhặt");

            yield return G.LoadScene(G.Level1);
            Component again = G.Comp(G.Player(), "ItemCollector");
            Assert.AreEqual(0, (int)G.Call(again, "GetKeys"), "Vào lại level nhưng Key không về 0");
        }
    }
}
