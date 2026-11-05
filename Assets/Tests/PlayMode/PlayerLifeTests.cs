using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DungeonEscape.Tests
{
    /// <summary>Mạng sống, chết, hồi sinh, bẫy: TC-PL-04, TC-PL-05, TC-PL-06, TC-TR-01 (bảng quyết định 4.4).</summary>
    public class PlayerLifeTests : GameTestBase
    {
        private GameObject player;
        private Component life;

        private IEnumerator EnterLevel1(int lives)
        {
            G.FreshProgress(lives: lives);
            yield return G.LoadScene(G.Level1);
            player = G.Player();
            life = G.Comp(player, "PlayerLife");
            yield return new WaitForSeconds(1f);   // đứng yên ở điểm xuất phát
        }

        // Quy tắc 2: còn mạng, chưa qua checkpoint → hồi sinh tại điểm xuất phát
        [UnityTest]
        public IEnumerator TC_PL_04_ConMang_ChuaQuaCheckpoint_HoiSinhTaiDiemXuatPhat()
        {
            yield return EnterLevel1(lives: 3);
            Vector3 spawn = (Vector3)G.Get(life, "initSpawnPos");

            yield return G.KillPlayer(player);

            Assert.IsTrue(G.ColliderOn(player), "Còn mạng nhưng Player không hồi sinh");
            Assert.AreEqual(2, G.Lives, "Chết 1 lần nhưng số mạng không giảm từ 3 xuống 2");
            Assert.AreEqual("2", G.TextOf(G.Get(life, "heartsCount")), "HUD không cập nhật số mạng");
            Assert.Less(Vector2.Distance(player.transform.position, spawn), 1f, "Không hồi sinh tại điểm xuất phát");
        }

        // Quy tắc 1: còn mạng, đã qua checkpoint → hồi sinh tại checkpoint
        [UnityTest]
        public IEnumerator TC_PL_05_ConMang_DaQuaCheckpoint_HoiSinhTaiCheckpoint()
        {
            yield return EnterLevel1(lives: 3);
            Component manager = G.Find("CheckpointManager");
            Transform[] points = (Transform[])G.Get(manager, "CheckPoints");

            // Chọn checkpoint thật đầu tiên (phần tử 0 là điểm khởi đầu)
            Transform checkpoint = null;
            foreach (Object o in G.FindAll("Checkpoint"))
            {
                Transform t = ((Component)o).transform;
                if (System.Array.IndexOf(points, t) > 0) { checkpoint = t; break; }
            }
            Assert.IsNotNull(checkpoint, "Level1 không có checkpoint nào trong CheckpointManager");

            G.Teleport(player, checkpoint.position);
            yield return G.WaitUntil(() => (bool)G.Call(manager, "hasPassedAnyCheckPoints"), 2f);
            Assert.IsTrue((bool)G.Call(manager, "hasPassedAnyCheckPoints"), "Đi qua checkpoint nhưng không được ghi nhận");

            Vector3 expected = checkpoint.position + (Vector3)G.Get(life, "respawnOffset");
            yield return G.KillPlayer(player);

            Assert.IsTrue(G.ColliderOn(player), "Còn mạng nhưng Player không hồi sinh");
            Assert.Less(Vector2.Distance(player.transform.position, expected), 1f, "Không hồi sinh tại checkpoint đã qua");
        }

        // Quy tắc 3: hết mạng → Game Over
        [UnityTest]
        public IEnumerator TC_PL_06_ConMotMang_Chet_MoGameOver()
        {
            yield return EnterLevel1(lives: 1);

            yield return G.KillPlayer(player);

            Component gameOver = G.Find("GameOverMenu");
            Assert.AreEqual(0, G.Lives, "Mạng không về 0");
            Assert.IsTrue(G.CanvasEnabled(gameOver, "gameOverCanvas"), "Hết mạng nhưng không hiện Game Over");
            Assert.AreEqual(0f, Time.timeScale, "Game Over hiện nhưng game không dừng");
            Assert.IsFalse(((GameObject)G.Get(gameOver, "startLevelAgainBtn")).activeSelf,
                "Hết mạng nhưng nút Start again vẫn hiện");
        }

        // Phân vùng tương đương: mỗi loại vật nguy hiểm trong Level1 phải làm chết và trừ đúng 1 mạng
        [UnityTest]
        public IEnumerator TC_TR_01_MoiLoaiBay_LamChetVaTruDung1Mang()
        {
            yield return EnterLevel1(lives: 20);
            string[] hazards = { "SpikeTrap", "SawTrap", "DeathZone" };
            int tested = 0;

            foreach (string hazard in hazards)
            {
                Object[] found = G.FindAll(hazard);
                if (found.Length == 0) { Debug.LogWarning($"Level1 không có {hazard}, bỏ qua"); continue; }

                Component h = (Component)found[0];
                Collider2D col = h.GetComponent<Collider2D>();
                Vector2 target = col != null ? (Vector2)col.bounds.center : (Vector2)h.transform.position;
                int before = G.Lives;

                G.Teleport(player, target);
                yield return G.WaitUntil(() => !G.ColliderOn(player), 2f);
                Assert.IsFalse(G.ColliderOn(player), $"Chạm {hazard} nhưng Player không chết");
                yield return G.WaitUntil(() => G.ColliderOn(player), 6f);

                Assert.AreEqual(before - 1, G.Lives, $"Chạm {hazard}: số mạng không giảm đúng 1");
                tested++;
                yield return new WaitForSeconds(0.5f);
            }
            Assert.Greater(tested, 0, "Level1 không có loại bẫy nào để kiểm tra");
        }
    }
}
