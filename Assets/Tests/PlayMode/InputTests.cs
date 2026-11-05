using System.Collections;
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
    /// Test có giả lập bàn phím (Input System Test Framework): TC-PL-01, TC-PL-02, TC-UI-03.
    /// InputTestFixture tạo một Input System riêng cho mỗi test và thêm bàn phím, chuột ảo,
    /// nên test không phụ thuộc thiết bị thật đang cắm vào máy.
    /// </summary>
    public class InputTests : InputTestFixture
    {
        private Keyboard keyboard;

        [OneTimeSetUp] public void SaveRealPrefs() => G.BackupPrefs();
        [OneTimeTearDown] public void RestoreRealPrefs() => G.RestorePrefs();

        public override void Setup()
        {
            base.Setup();
            G.FreshProgress();
            // Scheme "Keyboard&Mouse" của PlayerInput cần cả bàn phím và chuột
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            // Hủy object của scene TRƯỚC khi fixture trả lại Input System thật. Nếu để scene tự unload ở test
            // sau, PlayerInput/OnScreenControl sẽ OnDisable trên Input System đã bị reset và giữ lại thiết bị
            // ảo đã xóa -> lỗi "Cannot be negative", "statePtr", "control" lan sang các test chạy sau.
            DestroyInputObjects();
            base.TearDown();
        }

        private static void DestroyInputObjects()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
            }
            // Object DontDestroyOnLoad không thuộc scene nào ở trên, hủy riêng nếu có dùng Input System
            DestroyAll<OnScreenControl>();
            DestroyAll<PlayerInput>();
            DestroyAll<InputSystemUIInputModule>();
        }

        private static void DestroyAll<T>() where T : Component
        {
            foreach (T c in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c != null) Object.DestroyImmediate(c.gameObject);
        }

        private IEnumerator EnterLevel1(int character = 0)
        {
            PlayerPrefs.SetInt("Character", character);
            yield return G.LoadScene(G.Level1);
            yield return new WaitForSeconds(1f);
        }

        private IEnumerator PressJump()
        {
            PressAndRelease(keyboard.spaceKey);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TC_PL_01_GiuD_DiPhai_GiuA_DiTrai()
        {
            yield return EnterLevel1();
            GameObject player = G.Player();
            SpriteRenderer sprite = player.GetComponent<SpriteRenderer>();

            float x0 = player.transform.position.x;
            Press(keyboard.dKey);
            yield return new WaitForSeconds(0.25f);
            Release(keyboard.dKey);
            yield return null;
            Assert.Greater(player.transform.position.x, x0 + 0.3f, "Giữ D nhưng Player không đi sang phải");
            Assert.IsFalse(sprite.flipX, "Đi sang phải nhưng sprite quay sang trái");

            float x1 = player.transform.position.x;
            Press(keyboard.aKey);
            yield return new WaitForSeconds(0.25f);
            Release(keyboard.aKey);
            yield return null;
            Assert.Less(player.transform.position.x, x1 - 0.3f, "Giữ A nhưng Player không đi sang trái");
            Assert.IsTrue(sprite.flipX, "Đi sang trái nhưng sprite không quay sang trái");
        }

        /// <summary>
        /// Phân tích giá trị biên: nhảy từ đất 1 lần + nhảy trên không đúng maxAirJumpCnt lần;
        /// lần thứ maxAirJumpCnt + 1 trên không phải không có tác dụng.
        /// </summary>
        private IEnumerator CheckJumpLimit(int character)
        {
            yield return EnterLevel1(character);
            GameObject player = G.Player();
            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            Component movement = G.Comp(player, "PlayerMovement");
            float jumpForce = (float)G.Get(movement, "jumpForce");
            float airForce = (float)G.Get(movement, "extraJumpForce");
            int airJumps = (int)G.Get(movement, "maxAirJumpCnt");

            yield return PressJump();
            Assert.Greater(rb.linearVelocity.y, jumpForce * 0.8f, "Nhảy từ mặt đất không có tác dụng");

            for (int i = 1; i <= airJumps; i++)
            {
                yield return G.WaitUntil(() => rb.linearVelocity.y < airForce * 0.4f, 3f);
                yield return PressJump();
                Assert.Greater(rb.linearVelocity.y, airForce * 0.8f, $"Lần nhảy trên không thứ {i}/{airJumps} không có tác dụng");
            }

            yield return G.WaitUntil(() => rb.linearVelocity.y < airForce * 0.4f, 3f);
            yield return PressJump();
            Assert.Less(rb.linearVelocity.y, airForce * 0.6f,
                $"Nhảy trên không được {airJumps + 1} lần, vượt giới hạn {airJumps} của nhân vật");
        }

        [UnityTest]
        public IEnumerator TC_PL_02a_VirtualGuy_KhongNhayDuocTrenKhong() => CheckJumpLimit(character: 0);

        [UnityTest]
        public IEnumerator TC_PL_02b_NinjaFrog_NhayTrenKhongDung2Lan() => CheckJumpLimit(character: 2);

        [UnityTest]
        public IEnumerator TC_UI_03_PhimP_TamDungTiepTuc_HomeVeStartScreen()
        {
            yield return EnterLevel1();
            Component pause = G.Find("PauseManager");

            PressAndRelease(keyboard.pKey);
            yield return null;
            yield return null;
            Assert.AreEqual(0f, Time.timeScale, "Bấm P nhưng game không dừng");
            Assert.IsTrue(G.CanvasEnabled(pause, "pauseCanvas"), "Bấm P nhưng không hiện menu Pause");

            PressAndRelease(keyboard.pKey);
            yield return null;
            yield return null;
            Assert.AreEqual(1f, Time.timeScale, "Bấm P lần nữa nhưng game không chạy tiếp");
            Assert.IsFalse(G.CanvasEnabled(pause, "pauseCanvas"), "Bấm P lần nữa nhưng menu Pause không đóng");

            PressAndRelease(keyboard.pKey);
            yield return null;
            yield return null;
            // Nút cảm ứng ảo (OnScreenControl) bị unload cùng scene khi về Home sẽ báo lỗi "statePtr" bên trong
            // InputTestFixture (không xảy ra khi chơi thật). Test này không kiểm tra nút cảm ứng nên hủy trước.
            DestroyAll<OnScreenControl>();
            G.Call(pause, "HomeMenu");
            yield return G.WaitUntil(() => G.ActiveIndex == G.StartScreen, 5f);
            Assert.AreEqual(G.StartScreen, G.ActiveIndex, "Chọn Home nhưng không về Start Screen");
            Assert.AreEqual(1f, Time.timeScale, "Về Start Screen nhưng game vẫn bị dừng");
        }
    }
}
