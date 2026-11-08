using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DungeonEscape.Tests
{
    /// <summary>Test chức năng ở Start Screen: TC-UI-01, TC-UI-02, TC-UI-07, TC-IT-03.</summary>
    public class MenuTests : GameTestBase
    {
        [UnityTest]
        public IEnumerator TC_UI_01_NguoiChoiMoi_HienSTART_VaVaoLevel1()
        {
            yield return G.LoadScene(G.StartScreen);
            Component menu = G.Find("StartMenu");
            Assert.AreEqual("START", G.TextOf(G.Get(menu, "startBtnText")), "Người chơi mới nhưng nút chính không ghi START");

            G.Call(menu, "StartGame");
            yield return G.WaitUntil(() => G.ActiveIndex == G.Level1, 5f);
            Assert.AreEqual(G.Level1, G.ActiveIndex, "Bấm START nhưng không vào Level1");
        }

        [UnityTest]
        public IEnumerator TC_UI_01b_DaMoLevel2_HienCONTINUE_VaVaoLevel2()
        {
            PlayerPrefs.SetInt("Level1", 2);   // Completed
            PlayerPrefs.SetInt("Level2", 1);   // Unlocked
            yield return G.LoadScene(G.StartScreen);
            Component menu = G.Find("StartMenu");
            Assert.AreEqual("CONTINUE", G.TextOf(G.Get(menu, "startBtnText")), "Đã mở Level2 nhưng nút chính không ghi CONTINUE");

            G.Call(menu, "StartGame");
            yield return G.WaitUntil(() => G.ActiveIndex == G.Level2, 5f);
            Assert.AreEqual(G.Level2, G.ActiveIndex, "Bấm CONTINUE nhưng không vào Level2");
        }

        /// <summary>Đọc trạng thái nút level theo tên level (Level1, Level2...).</summary>
        private static bool ButtonOpen(string levelName)
        {
            foreach (Object o in G.FindAll("LevelLoader"))
            {
                if (G.Get(o, "levelName").ToString() == levelName)
                    return ((Button)G.Get(o, "levelBtn")).interactable;
            }
            Assert.Fail($"Menu Levels không có nút cho {levelName}");
            return false;
        }

        [UnityTest]
        public IEnumerator TC_UI_02_NutLevel_KhoaVaMoTheoTienTrinh()
        {
            yield return G.LoadScene(G.StartScreen);
            Assert.IsTrue(ButtonOpen("Level1"), "Người chơi mới nhưng Level1 bị khóa");
            Assert.IsFalse(ButtonOpen("Level2"), "Chưa hoàn thành Level1 mà Level2 đã mở");
            Assert.IsFalse(ButtonOpen("Level3"), "Level3 mở quá sớm");

            // Giả lập đã hoàn thành Level1
            PlayerPrefs.SetInt("Level1", 2);
            PlayerPrefs.SetInt("Level2", 1);
            yield return G.LoadScene(G.StartScreen);
            Assert.IsTrue(ButtonOpen("Level1"), "Level1 đã hoàn thành nhưng không chơi lại được");
            Assert.IsTrue(ButtonOpen("Level2"), "Đã hoàn thành Level1 nhưng Level2 vẫn khóa");
            Assert.IsFalse(ButtonOpen("Level3"), "Chưa hoàn thành Level2 mà Level3 đã mở");
        }

        [UnityTest]
        public IEnumerator TC_UI_07_HetMang_BamSTART_HienInsufficientLives()
        {
            G.FreshProgress(lives: 0);
            yield return G.LoadScene(G.StartScreen);

            G.Call(G.Find("StartMenu"), "StartGame");
            yield return new WaitForSecondsRealtime(0.5f);

            Assert.AreEqual(G.StartScreen, G.ActiveIndex, "Hết mạng nhưng vẫn vào được level");
            Assert.IsTrue(G.CanvasEnabled(G.Find("InsufficientLivesMenu"), "canvas"),
                "Hết mạng nhưng không hiện menu Insufficient Lives");
        }

        [UnityTest]
        public IEnumerator TC_IT_03_DoiFoods_2KhongDuMua_3MuaDuoc1Mang()
        {
            G.FreshProgress(lives: 1, foods: 2);
            yield return G.LoadScene(G.StartScreen);
            Component shop = G.TryFind("ExtraLivesMenu", includeInactive: false);   // cần object đang bật (đã chạy Awake)
            if (shop == null) Assert.Inconclusive("Start Screen không có ExtraLivesMenu đang bật");

            // Biên dưới: 2 Foods không đủ đổi 1 mạng
            G.Call(shop, "OpenMenu");
            Assert.IsFalse(((Button)G.Get(shop, "increaseLifeCountBtn")).interactable,
                "Chỉ có 2 Foods mà nút tăng mạng vẫn bấm được");

            // Đúng ngưỡng: 3 Foods đổi được 1 mạng
            PlayerPrefs.SetInt("Foods", 3);
            G.Call(shop, "OpenMenu");
            Assert.IsTrue(((Button)G.Get(shop, "increaseLifeCountBtn")).interactable,
                "Có đủ 3 Foods nhưng nút tăng mạng bị khóa");
            G.Call(shop, "IncreaseBuyingLifeCount");
            G.Call(shop, "GetExtraLives");

            Assert.AreEqual(2, G.Lives, "Đổi 3 Foods nhưng số mạng không tăng đúng 1");
            Assert.AreEqual(0, PlayerPrefs.GetInt("Foods"), "Đổi mạng nhưng Foods không bị trừ đúng 3");
        }
    }
}
