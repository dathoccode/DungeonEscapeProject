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
    /// Kiểm thử chuyển trạng thái (State Transition Testing) của Player và Animator: TC-ST-01 .. TC-ST-19.
    ///
    /// Trạng thái lấy từ enum MovementState trong PlayerMovement.cs (tham số Int "state" của Animator):
    /// 0 Idle, 1 Run, 2 Jump, 3 Fall, 4 DoubleJump, 5 WallSliding; cộng thêm Death (trigger "death").
    /// Mỗi test kiểm tra cả Animator (tham số + state đang chạy) lẫn vật lý (vận tốc, vị trí).
    ///
    /// Để không phụ thuộc thiết kế màn chơi, nhiều test dựng một "sân thử" riêng (sàn, tường trên layer Ground)
    /// ở xa map Level1. Các object này chỉ tạo lúc chạy test, không sửa file nào của game.
    /// </summary>
    public class PlayerStateTests : InputTestFixture
    {
        // Giá trị của enum MovementState (PlayerMovement.cs)
        private const int IDLE = 0, RUN = 1, JUMP = 2, FALL = 3, DJUMP = 4, WALL = 5;
        private static readonly string[] AnimOf = { "Idle", "Run", "Jump", "Fall", "DoubleJump", "WallSliding" };
        private static readonly string[] AllAnims = { "Idle", "Run", "Jump", "Fall", "DoubleJump", "WallSliding", "Death" };

        // Thứ tự nhân vật trong CharacterData.asset (PlayerPrefs "Character")
        private const int VirtualGuy = 0, PinkMan = 1, NinjaFrog = 2, MaskDude = 3;
        private static readonly string[] CharName = { "VirtualGuy", "PinkMan", "NinjaFrog", "MaskDude" };

        // Sân thử: mặt sàn ở y = ArenaY, xa map Level1
        private const float ArenaX = 1000f, ArenaY = 300f;

        private Keyboard keyboard;
        private Mouse mouse;
        private int jumpCount, pauseCount;   // số lần action Jump / Pause của PlayerInput được kích hoạt
        private GameObject player;
        private Rigidbody2D rb;
        private Animator anim;
        private BoxCollider2D box;
        private SpriteRenderer sprite;
        private Component movement;

        [OneTimeSetUp] public void SaveRealPrefs() => G.BackupPrefs();
        [OneTimeTearDown] public void RestoreRealPrefs() => G.RestorePrefs();

        public override void Setup()
        {
            base.Setup();
            G.FreshProgress();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            // Giống InputTests: hủy object của scene TRƯỚC khi fixture trả lại Input System thật
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
            DestroyAll<OnScreenControl>();
            DestroyAll<PlayerInput>();
            DestroyAll<InputSystemUIInputModule>();
        }

        private static void DestroyAll<T>() where T : Component
        {
            foreach (T c in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c != null) Object.DestroyImmediate(c.gameObject);
        }

        // ================= Chuẩn bị =================

        private IEnumerator EnterLevel1(int character = VirtualGuy, int lives = 3)
        {
            PlayerPrefs.SetInt("Character", character);
            PlayerPrefs.SetInt("LifeCount", lives);
            yield return G.LoadScene(G.Level1);
            yield return new WaitForSeconds(1f);
            // Nút cảm ứng ảo (OnScreenControl) tạo một Gamepad ảo; PlayerInput có thể tự chuyển qua lại giữa scheme
            // Gamepad và Keyboard&Mouse làm mất phím bấm giả lập. Các test này chỉ dùng bàn phím nên (chỉ trong lúc test):
            // hủy nút cảm ứng như TC_UI_03, ghép PlayerInput cố định với bàn phím/chuột ảo và tắt tự chuyển scheme.
            DestroyAll<OnScreenControl>();
            yield return null;
            PlayerInput playerInput = Object.FindAnyObjectByType<PlayerInput>();
            Assert.IsNotNull(playerInput, "Level1 không có PlayerInput");
            playerInput.neverAutoSwitchControlSchemes = true;
            playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", keyboard, mouse);
            jumpCount = pauseCount = 0;
            playerInput.actions.FindAction("Player/Jump").performed += _ => jumpCount++;
            playerInput.actions.FindAction("Player/Pause").performed += _ => pauseCount++;
            playerInput.actions.FindAction("UI/Pause").performed += _ => pauseCount++;
            yield return null;
            // Lần nhấn đầu tiên sau khi ghép thiết bị đôi khi không tới được PlayerInput: nhấn thử D một lần
            // (Player sẽ được đặt lại vị trí ở bước sau nên không ảnh hưởng kết quả)
            yield return Tap(keyboard.dKey);
            yield return null;
            player = G.Player();
            rb = player.GetComponent<Rigidbody2D>();
            box = player.GetComponent<BoxCollider2D>();
            sprite = player.GetComponent<SpriteRenderer>();
            movement = G.Comp(player, "PlayerMovement");
            anim = (Animator)G.Get(movement, "animator");
            Assert.IsNotNull(anim, "PlayerMovement không có Animator");
        }

        /// <summary>Tạo một khối đặc (sàn/tường) trên layer Ground, giống Terrain của game.</summary>
        private static GameObject Block(string name, Vector2 center, Vector2 size)
        {
            GameObject go = new GameObject(name) { layer = LayerMask.NameToLayer("Ground") };
            go.transform.position = center;
            go.AddComponent<BoxCollider2D>().size = size;
            Physics2D.SyncTransforms();
            return go;
        }

        /// <summary>Sàn dài 80 đơn vị, mặt trên ở y = top.</summary>
        private static void Floor(float centerX, float top, float width = 80f) =>
            Block("TestFloor", new Vector2(centerX, top - 0.5f), new Vector2(width, 1f));

        /// <summary>Tường cao 12, mặt trái ở x = leftFace.</summary>
        private static void WallAt(float leftFace) =>
            Block("TestWall", new Vector2(leftFace + 0.5f, ArenaY + 6f), new Vector2(1f, 12f));

        /// <summary>Đặt Player đứng yên trên sàn và chờ ổn định ở Idle.</summary>
        private IEnumerator PlaceOnFloor(float x, float top = ArenaY)
        {
            G.Teleport(player, new Vector2(x, top + 1.1f));
            yield return WaitFor(() => OnFloor(top) && State == IDLE && AnimIs("Idle"), 3f);
            Assert.IsTrue(OnFloor(top) && AnimIs("Idle"),
                $"Không đặt được Player đứng yên trên sàn thử (chân y={Feet:F2}, v={rb.linearVelocity}, {Now})");
        }

        // ================= Đọc trạng thái =================

        private int State => anim.GetInteger("state");
        private bool AnimIs(string n) => anim.GetCurrentAnimatorStateInfo(0).IsName(n);
        private float Feet => box.bounds.min.y;
        private float Speed => (float)G.Get(movement, "moveSpeed");
        private bool OnFloor(float top) => Mathf.Abs(Feet - top) < 0.06f && Mathf.Abs(rb.linearVelocity.y) < 0.05f;

        private string AnimName()
        {
            foreach (string n in AllAnims) if (AnimIs(n)) return n;
            return "?";
        }

        private string Now => $"animator={AnimName()}, state={State}, v=({rb.linearVelocity.x:F2}, {rb.linearVelocity.y:F2}), " +
                              $"pos=({player.transform.position.x:F2}, {player.transform.position.y:F2}), dirX={G.Get(movement, "dirX")}, " +
                              $"timeScale={Time.timeScale}, LifeCount={PlayerPrefs.GetInt("LifeCount", -1)}";

        /// <summary>
        /// Chờ tới khi điều kiện đúng, tối đa timeout giây thời gian thực. Khác G.WaitUntil: đo bằng đồng hồ thực và
        /// luôn chờ ít nhất 10 frame, để một frame bị khựng lâu (dọn bộ nhớ, nạp asset) không làm hết giờ ngay lập tức.
        /// </summary>
        private static IEnumerator WaitFor(System.Func<bool> condition, float timeout)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int frames = 0;
            while (!condition() && (sw.Elapsed.TotalSeconds < timeout || frames < 10))
            {
                frames++;
                yield return null;
            }
        }

        /// <summary>Chờ tới khi animator ở yên một state liên tục trong stable giây.</summary>
        private IEnumerator WaitAnimStable(string name, float stable, float timeout)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var since = System.Diagnostics.Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < timeout)
            {
                if (!AnimIs(name)) since.Restart();
                else if (since.Elapsed.TotalSeconds >= stable) yield break;
                yield return null;
            }
            Assert.Fail($"Animator không ổn định ở {name} sau {timeout}s ({Now})");
        }

        private IEnumerator WaitAnim(string name, float timeout = 1.5f, string why = "")
        {
            yield return WaitFor(() => AnimIs(name), timeout);
            Assert.IsTrue(AnimIs(name), $"Không chuyển sang {name} sau {timeout}s {why}({Now})");
        }

        /// <summary>
        /// Chờ n bước vật lý rồi chờ thêm 1 frame thường. Ngay sau WaitForFixedUpdate, Input System đang ở ngữ cảnh
        /// fixed-update (không có bộ đệm trạng thái vì game xử lý input ở Update), nếu gọi Press/Release lúc đó sẽ lỗi
        /// "does not have an associated state".
        /// </summary>
        private static IEnumerator FixedSteps(int n)
        {
            for (int i = 0; i < n; i++) yield return new WaitForFixedUpdate();
            yield return null;
        }

        /// <summary>Nhấn phím, giữ 2 frame rồi nhả (PressAndRelease trong cùng 1 lần cập nhật input đôi khi bị bỏ qua).</summary>
        private IEnumerator Tap(UnityEngine.InputSystem.Controls.KeyControl key)
        {
            Press(key);
            yield return null;
            yield return null;
            Release(key);
            yield return null;
        }

        private float DirX => (float)G.Get(movement, "dirX");

        /// <summary>
        /// Phím giả lập của InputTestFixture thỉnh thoảng không tới được PlayerInput (lỗi môi trường test, không xảy ra
        /// với bàn phím thật). Hàm này nhấn phím và kiểm tra input đã tới game (dirX của PlayerMovement hoặc số lần action
        /// được kích hoạt); nếu chưa thì nhả ra và nhấn lại, tối đa 3 lần. Không kiểm tra phản ứng của game ở đây.
        /// </summary>
        private IEnumerator PressUntil(UnityEngine.InputSystem.Controls.KeyControl key, System.Func<bool> reached)
        {
            for (int i = 0; i < 3; i++)
            {
                Press(key);
                yield return WaitFor(reached, 0.3f);
                if (reached()) yield break;
                Release(key);
                yield return null;
                yield return null;
            }
            Assert.Fail($"Môi trường test: nhấn {key.name} 3 lần nhưng input không tới PlayerInput");
        }

        private IEnumerator ReleaseUntil(UnityEngine.InputSystem.Controls.KeyControl key, System.Func<bool> reached)
        {
            for (int i = 0; i < 3; i++)
            {
                Release(key);
                yield return WaitFor(reached, 0.3f);
                if (reached()) yield break;
                Press(key);
                yield return null;
                yield return null;
            }
            Assert.Fail($"Môi trường test: nhả {key.name} 3 lần nhưng input không tới PlayerInput");
        }

        // Giữ D / A: input tới game khi dirX đổi đúng (giữ cả A và D thì dirX = 0)
        private IEnumerator HoldD() => PressUntil(keyboard.dKey, () => keyboard.aKey.isPressed ? Mathf.Abs(DirX) < 0.01f : DirX > 0.5f);
        private IEnumerator HoldA() => PressUntil(keyboard.aKey, () => keyboard.dKey.isPressed ? Mathf.Abs(DirX) < 0.01f : DirX < -0.5f);
        private IEnumerator LetGoD() => ReleaseUntil(keyboard.dKey, () => keyboard.aKey.isPressed ? DirX < -0.5f : Mathf.Abs(DirX) < 0.01f);
        private IEnumerator LetGoA() => ReleaseUntil(keyboard.aKey, () => keyboard.dKey.isPressed ? DirX > 0.5f : Mathf.Abs(DirX) < 0.01f);

        /// <summary>Bấm Space: chỉ coi là đã bấm khi action Jump của PlayerInput thực sự được kích hoạt.</summary>
        private IEnumerator PressJump()
        {
            int before = jumpCount;
            yield return PressUntil(keyboard.spaceKey, () => jumpCount > before);
            Release(keyboard.spaceKey);
            yield return null;
        }

        private IEnumerator PressP()
        {
            int before = pauseCount;
            yield return PressUntil(keyboard.pKey, () => pauseCount > before);
            Release(keyboard.pKey);
            yield return null;
            yield return null;
        }

        /// <summary>Gọi action mỗi frame trong một khoảng thời gian thực (vẫn chạy khi timeScale = 0).</summary>
        private static IEnumerator Sample(float seconds, System.Action each)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < seconds)
            {
                each();
                yield return null;
            }
        }

        // ================= Chuyển trạng thái hợp lệ =================

        [UnityTest, Timeout(60000)]
        public IEnumerator TC_ST_01_Idle_GiuD_Run_ThaD_Idle()
        {
            yield return EnterLevel1();
            Floor(ArenaX, ArenaY);
            yield return PlaceOnFloor(ArenaX - 20f);
            Assert.AreEqual(IDLE, State);
            Assert.Less(Mathf.Abs(rb.linearVelocity.x), 0.05f, "Đứng yên nhưng vẫn có vận tốc ngang");

            yield return HoldD();
            yield return WaitAnim("Run");
            Assert.AreEqual(RUN, State, Now);
            // Animator đổi trong Update, vận tốc đổi ở FixedUpdate kế tiếp: chờ 2 bước vật lý rồi mới đo
            yield return FixedSteps(2);
            Assert.Greater(rb.linearVelocity.x, Speed * 0.9f, "Ở Run nhưng không chạy sang phải với đúng tốc độ. " + Now);

            yield return LetGoD();
            yield return WaitAnim("Idle");
            Assert.AreEqual(IDLE, State, Now);
            yield return FixedSteps(2);
            Assert.Less(Mathf.Abs(rb.linearVelocity.x), 0.05f, "Thả D nhưng Player vẫn trượt. " + Now);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TC_ST_02_Idle_Space_Jump_Fall_TiepDat_Idle()
        {
            yield return EnterLevel1();
            Floor(ArenaX, ArenaY);
            yield return PlaceOnFloor(ArenaX);
            float jumpForce = (float)G.Get(movement, "jumpForce");

            yield return PressJump();
            yield return WaitAnim("Jump");
            yield return FixedSteps(2);
            Assert.AreEqual(JUMP, State, Now);
            Assert.Greater(rb.linearVelocity.y, 0f, "Ở Jump nhưng không đi lên. " + Now);
            Assert.Greater(Feet, ArenaY + 0.05f, "Ở Jump nhưng chân vẫn trên sàn");

            yield return WaitAnim("Fall", 3f);
            Assert.AreEqual(FALL, State, Now);
            Assert.Less(rb.linearVelocity.y, 0f, "Ở Fall nhưng không rơi xuống. " + Now);

            yield return WaitFor(() => OnFloor(ArenaY) && AnimIs("Idle"), 3f);
            Assert.IsTrue(OnFloor(ArenaY), $"Không tiếp đất (chân y={Feet:F2}). " + Now);
            Assert.IsTrue(AnimIs("Idle") && State == IDLE, "Tiếp đất nhưng không về Idle. " + Now);
            Assert.Greater(jumpForce, 0f);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TC_ST_03_Run_Space_Jump_TiepDatKhiGiuD_Run()
        {
            yield return EnterLevel1();
            Floor(ArenaX, ArenaY);
            yield return PlaceOnFloor(ArenaX - 30f);

            yield return HoldD();
            yield return WaitAnim("Run");
            yield return WaitFor(() => rb.linearVelocity.x > Speed * 0.9f, 1f);
            yield return PressJump();
            yield return WaitAnim("Jump");
            yield return FixedSteps(2);
            Assert.AreEqual(JUMP, State, Now);
            Assert.Greater(rb.linearVelocity.x, Speed * 0.9f, "Nhảy khi đang chạy nhưng mất vận tốc ngang. " + Now);
            Assert.Greater(rb.linearVelocity.y, 0f, Now);

            yield return WaitAnim("Fall", 3f);
            yield return WaitFor(() => OnFloor(ArenaY) && AnimIs("Run"), 3f);
            Assert.IsTrue(OnFloor(ArenaY), "Không tiếp đất. " + Now);
            Assert.IsTrue(AnimIs("Run") && State == RUN, "Tiếp đất khi vẫn giữ D nhưng không về Run. " + Now);
            yield return FixedSteps(2);   // bước tiếp đất có ma sát với sàn làm giảm v.x trong 1 bước
            Assert.Greater(rb.linearVelocity.x, Speed * 0.9f, "Tiếp đất xong không chạy tiếp. " + Now);
            yield return LetGoD();
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TC_ST_04_PinkMan_Jump_DoubleJump_Fall_Idle()
        {
            yield return EnterLevel1(PinkMan);
            Floor(ArenaX, ArenaY);
            yield return PlaceOnFloor(ArenaX);
            float airForce = (float)G.Get(movement, "extraJumpForce");

            yield return PressJump();
            yield return WaitAnim("Jump");
            yield return WaitFor(() => rb.linearVelocity.y < airForce * 0.4f, 3f);
            yield return PressJump();
            yield return WaitAnim("DoubleJump");
            Assert.AreEqual(DJUMP, State, Now);
            Assert.Greater(rb.linearVelocity.y, airForce * 0.8f, "Ở DoubleJump nhưng không bật lên lần 2. " + Now);

            yield return WaitAnim("Fall", 3f);
            Assert.AreEqual(FALL, State, Now);
            Assert.Less(rb.linearVelocity.y, 0f, Now);

            yield return WaitFor(() => OnFloor(ArenaY) && AnimIs("Idle"), 4f);
            Assert.IsTrue(OnFloor(ArenaY) && AnimIs("Idle") && State == IDLE, "Nhảy đôi xong không về Idle. " + Now);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TC_ST_05_Run_RaKhoiMep_Fall_TiepDat_Idle()
        {
            yield return EnterLevel1();
            // Gờ hẹp (mép phải ở x = ArenaX + 3) và sàn thấp hơn 6 đơn vị bên dưới
            Floor(ArenaX, ArenaY, width: 6f);
            float low = ArenaY - 6f;
            Floor(ArenaX + 10f, low, width: 40f);
            yield return PlaceOnFloor(ArenaX);

            yield return HoldD();
            yield return WaitAnim("Run");
            yield return WaitAnim("Fall", 3f, "khi chạy ra khỏi mép ");
            Assert.AreEqual(FALL, State, Now);
            Assert.Less(rb.linearVelocity.y, 0f, Now);
            yield return LetGoD();

            yield return WaitFor(() => OnFloor(low) && AnimIs("Idle"), 4f);
            Assert.IsTrue(OnFloor(low), $"Không tiếp đất ở sàn dưới (chân y={Feet:F2}). " + Now);
            Assert.IsTrue(AnimIs("Idle") && State == IDLE, Now);
        }

        /// <summary>MaskDude rơi sát tường, giữ D hướng vào tường: Fall -> WallSliding.</summary>
        private IEnumerator MaskDudeToWall()
        {
            yield return EnterLevel1(MaskDude);
            Floor(ArenaX, ArenaY);
            WallAt(ArenaX + 2.5f);
            G.Teleport(player, new Vector2(ArenaX + 1.2f, ArenaY + 6f));
            yield return null;
            yield return HoldD();
            yield return WaitAnim("WallSliding", 2f, "khi rơi sát tường ");
            Assert.AreEqual(WALL, State, Now);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TC_ST_06_MaskDude_Fall_WallSliding_TiepDat_Idle()
        {
            yield return MaskDudeToWall();
            float slide = (float)G.Get(movement, "wallSlideSpeed");
            // Đo tốc độ trượt trung bình qua quãng đường (vận tốc tức thời bị ma sát ở lần chạm tường đầu tiên
            // và trọng lực trong bước vật lý làm lệch, nên không so trực tiếp với wallSlideSpeed)
            yield return FixedSteps(5);
            float y0 = player.transform.position.y, t0 = Time.time;
            yield return FixedSteps(15);
            float avg = (player.transform.position.y - y0) / (Time.time - t0);
            Assert.AreEqual(WALL, State, Now);
            Assert.Less(avg, -slide * 0.5f, $"Bám tường nhưng không trượt xuống (tốc độ TB {avg:F2}). " + Now);
            Assert.Greater(avg, -slide * 2f, $"Bám tường nhưng rơi tự do (tốc độ TB {avg:F2}, wallSlideSpeed {slide}). " + Now);
            Assert.Less(Mathf.Abs(rb.linearVelocity.x), 0.5f, Now);

            yield return LetGoD();
            yield return null;
            Assert.AreEqual(WALL, State, "Thả D khi đang bám tường thì rời tường. " + Now);

            yield return WaitFor(() => OnFloor(ArenaY) && AnimIs("Idle"), 5f);
            Assert.IsTrue(OnFloor(ArenaY), "Trượt tường nhưng không xuống tới sàn. " + Now);
            Assert.IsTrue(AnimIs("Idle") && State == IDLE, "Trượt tường xuống tới sàn nhưng không về Idle. " + Now);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TC_ST_07_MaskDude_WallJump_KhongHienIdleGiuaKhong()
        {
            yield return MaskDudeToWall();
            yield return LetGoD();
            yield return null;
            float x0 = player.transform.position.x;

            yield return PressJump();
            yield return FixedSteps(1);
            Assert.Less(rb.linearVelocity.x, -5f, "Wall jump không đẩy Player ra khỏi tường. " + Now);
            Assert.Greater(rb.linearVelocity.y, 5f, "Wall jump không bật Player lên. " + Now);

            var bad = new List<string>();
            yield return Sample(0.3f, () =>
            {
                if (AnimIs("Idle") || AnimIs("Run") || State == IDLE || State == RUN)
                    bad.Add($"{Time.time:F2}s {Now}");
            });
            Assert.Less(player.transform.position.x, x0 - 0.5f, "Player không rời khỏi tường. " + Now);
            Assert.IsEmpty(bad, $"Đang bay lên sau wall jump nhưng animator ở Idle/Run {bad.Count} frame, ví dụ: "
                                + (bad.Count > 0 ? bad[0] : ""));
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TC_ST_08_Chet_Death_HoiSinh_IdleTaiDiemXuatPhat()
        {
            yield return EnterLevel1();
            Component life = G.Comp(player, "PlayerLife");
            Vector3 spawn = (Vector3)G.Get(life, "initSpawnPos");
            int lives = G.Lives;

            GameObject trap = G.SpawnTrapOn(player);
            yield return WaitFor(() => !G.ColliderOn(player), 2f);
            Assert.IsFalse(G.ColliderOn(player), "Chạm bẫy nhưng Player không chết");
            Object.Destroy(trap);
            yield return WaitAnim("Death", 1f);

            yield return WaitFor(() => G.ColliderOn(player), 5f);
            Assert.IsTrue(G.ColliderOn(player), "Không hồi sinh sau animation Death. " + Now);
            // Sau hồi sinh animator có thể nháy Fall 1 frame (xem TC_ST_20), nên chờ Idle ổn định 0,5 s
            yield return WaitAnimStable("Idle", 0.5f, 3f);

            Assert.AreEqual(IDLE, State, Now);
            Assert.Less(rb.linearVelocity.magnitude, 0.05f, "Hồi sinh xong Player không đứng yên. " + Now);
            Assert.AreEqual(spawn.x, player.transform.position.x, 0.3f, "Không hồi sinh tại điểm xuất phát");
            Assert.AreEqual(lives - 1, G.Lives, "Chết nhưng không trừ đúng 1 mạng");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TC_ST_09_ChetKhiDangNhay_GiuDeathDenHetClip_HoiSinhIdle()
        {
            yield return EnterLevel1();
            yield return PressJump();
            yield return WaitAnim("Jump");

            GameObject trap = G.SpawnTrapOn(player);
            yield return WaitFor(() => !G.ColliderOn(player), 2f);
            Assert.IsFalse(G.ColliderOn(player), "Chạm bẫy giữa không trung nhưng Player không chết");
            Object.Destroy(trap);
            yield return WaitAnim("Death", 1f);

            // Trong lúc chết, "state" vẫn đổi (Jump/Fall) nhưng Animator không được rời Death trước khi hết clip
            var left = new List<string>();
            yield return Sample(1f, () => { if (!AnimIs("Death") && !G.ColliderOn(player)) left.Add(Now); });
            Assert.IsEmpty(left, "Animator rời Death trước khi hết clip: " + (left.Count > 0 ? left[0] : ""));

            yield return WaitFor(() => G.ColliderOn(player), 5f);
            Assert.IsTrue(G.ColliderOn(player), "Không hồi sinh. " + Now);
            yield return WaitAnimStable("Idle", 0.5f, 3f);
            Assert.AreEqual(IDLE, State, Now);
            Assert.Less(rb.linearVelocity.magnitude, 0.05f, Now);
        }

        /// <summary>
        /// 0-switch coverage trên Animator Controller của 4 nhân vật đang dùng: với mọi cặp (state nguồn, giá trị
        /// "state" mới) phải chuyển sang đúng animation; trigger "death" từ mọi state phải vào Death; trong Death
        /// đổi "state" không được thoát; hết clip Death phải về Idle.
        /// Test này kiểm tra cấu hình Animator nên tắt PlayerMovement và dừng vật lý của Player (chỉ trong test).
        /// </summary>
        [UnityTest, Timeout(120000)]
        public IEnumerator TC_ST_10_MaTranChuyenTrangThaiAnimator_4NhanVat()
        {
            var fails = new List<string>();
            for (int c = 0; c < CharName.Length; c++)
            {
                yield return EnterLevel1(c);
                ((Behaviour)movement).enabled = false;
                rb.simulated = false;

                for (int src = 0; src < AnimOf.Length; src++)
                {
                    for (int v = 0; v < AnimOf.Length; v++)
                    {
                        if (v == src) continue;
                        Enter(src);
                        if (!AnimIs(AnimOf[src])) { fails.Add($"{CharName[c]}: không vào được {AnimOf[src]}"); break; }
                        anim.SetInteger("state", v);
                        anim.Update(0.02f);
                        anim.Update(0.02f);
                        if (!AnimIs(AnimOf[v]))
                            fails.Add($"{CharName[c]}: {AnimOf[src]} + state={v} -> {AnimName()} (mong đợi {AnimOf[v]})");
                    }

                    Enter(src);
                    anim.SetTrigger("death");
                    anim.Update(0.02f);
                    anim.Update(0.02f);
                    if (!AnimIs("Death")) fails.Add($"{CharName[c]}: {AnimOf[src]} + death -> {AnimName()} (mong đợi Death)");
                    anim.ResetTrigger("death");
                }

                // Trong Death: đổi state không được thoát; hết clip (1,83 s) phải về Idle
                Enter(IDLE);
                anim.SetTrigger("death");
                anim.Update(0.02f);
                for (int v = 0; v < AnimOf.Length; v++)
                {
                    anim.SetInteger("state", v);
                    anim.Update(0.2f);
                    if (!AnimIs("Death")) { fails.Add($"{CharName[c]}: Death + state={v} -> {AnimName()} trước khi hết clip"); break; }
                }
                anim.SetInteger("state", IDLE);
                for (int i = 0; i < 10; i++) anim.Update(0.1f);
                if (!AnimIs("Idle")) fails.Add($"{CharName[c]}: hết clip Death -> {AnimName()} (mong đợi Idle)");

                // Phép thử chỉ chạy trên Animator: vật lý của Player bị tắt nên Player không được rơi/trôi
                Assert.IsFalse(rb.simulated);
                Assert.Less(rb.linearVelocity.magnitude, 0.05f, "Player vẫn chuyển động trong lúc thử Animator");
            }
            Assert.IsEmpty(fails, $"{fails.Count} chuyển trạng thái Animator sai:\n" + string.Join("\n", fails));
        }

        private void Enter(int s)
        {
            anim.ResetTrigger("death");
            anim.SetInteger("state", s);
            anim.Play(AnimOf[s], 0, 0f);
            anim.Update(0f);
            anim.Update(0.02f);
        }

        // ================= Chuyển không hợp lệ và kẹt trạng thái =================

        [UnityTest, Timeout(60000)]
        public IEnumerator TC_ST_11_BamADCungLuc_Idle_ThaMotPhim_ChayTheoPhimConLai()
        {
            yield return EnterLevel1();
            Floor(ArenaX, ArenaY);
            yield return PlaceOnFloor(ArenaX);

            yield return HoldD();
            yield return WaitAnim("Run");
            yield return HoldA();
            yield return WaitAnim("Idle", 1f, "khi giữ cả A và D ");
            Assert.AreEqual(IDLE, State, Now);
            yield return FixedSteps(1);
            Assert.Less(Mathf.Abs(rb.linearVelocity.x), 0.1f, "Giữ cả A và D nhưng Player vẫn chạy. " + Now);

            yield return LetGoD();
            yield return WaitAnim("Run", 1f, "khi thả D, còn giữ A ");
            yield return FixedSteps(1);
            Assert.Less(rb.linearVelocity.x, -Speed * 0.9f, "Thả D (còn giữ A) nhưng không chạy sang trái. " + Now);
            Assert.IsTrue(sprite.flipX, "Chạy sang trái nhưng sprite không quay trái");

            yield return LetGoA();
            yield return WaitAnim("Idle");
            yield return FixedSteps(1);
            Assert.Less(Mathf.Abs(rb.linearVelocity.x), 0.1f, Now);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TC_ST_12_ChayVaoTuong_BiChan_KhongODangRun()
        {
            yield return EnterLevel1();
            Floor(ArenaX, ArenaY);
            WallAt(ArenaX + 2f);
            yield return PlaceOnFloor(ArenaX);

            yield return HoldD();
            yield return new WaitForSeconds(1f);
            float x0 = player.transform.position.x;
            int runFrames = 0, frames = 0;
            yield return Sample(0.5f, () => { frames++; if (AnimIs("Run")) runFrames++; });
            float dx = player.transform.position.x - x0;
            string now = Now;
            yield return LetGoD();

            Assert.Less(Mathf.Abs(dx), 0.05f, "Tường không chặn Player");
            Assert.AreEqual(0, runFrames,
                $"Bị tường chặn (vị trí không đổi) nhưng animator vẫn ở Run {runFrames}/{frames} frame. {now}");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TC_ST_13_NhayApTuongGiuD_KhongBiDinhTuong()
        {
            yield return EnterLevel1();
            Floor(ArenaX, ArenaY);
            WallAt(ArenaX + 2f);
            yield return PlaceOnFloor(ArenaX);

            yield return HoldD();
            yield return new WaitForSeconds(0.5f);
            yield return PressJump();
            yield return WaitFor(() => Feet > ArenaY + 0.3f, 1f);
            Assert.Greater(Feet, ArenaY + 0.3f, "Không nhảy lên được khi áp tường. " + Now);

            // Thời gian bay bình thường khoảng 2*jumpForce/(9.81*gravityScale) ~ 0,95 s
            float t = 0f, maxHang = 0f, hang = 0f;
            while (!OnFloor(ArenaY) && t < 3f)
            {
                hang = Mathf.Abs(rb.linearVelocity.y) < 0.2f ? hang + Time.deltaTime : 0f;
                maxHang = Mathf.Max(maxHang, hang);
                t += Time.deltaTime;
                yield return null;
            }
            string now = Now;
            yield return LetGoD();
            Assert.IsTrue(OnFloor(ArenaY),
                $"Player bị dính vào tường sau {t:F1}s, không rơi xuống sàn (chân y={Feet:F2}, đứng yên trên không {maxHang:F2}s). {now}");
            Assert.Less(t, 1.5f, $"Rơi xuống quá chậm ({t:F2}s), Player bị ma sát giữ trên tường. {now}");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TC_ST_14_NhayDoiRoiTiepDatKhiDangLen_PhaiVeIdle()
        {
            yield return EnterLevel1(PinkMan);
            Floor(ArenaX, ArenaY);
            yield return PlaceOnFloor(ArenaX);
            Vector2 stand = player.transform.position;
            float airForce = (float)G.Get(movement, "extraJumpForce");

            yield return PressJump();
            yield return WaitFor(() => rb.linearVelocity.y < airForce * 0.4f, 3f);
            yield return PressJump();
            yield return WaitAnim("DoubleJump");
            Assert.Greater(rb.linearVelocity.y, 0f, Now);

            // Mô phỏng chạm đất khi vẫn đang đi lên (ví dụ chân vướng mép platform): đặt Player đứng lại trên sàn
            G.Teleport(player, stand);
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(OnFloor(ArenaY), "Player không đứng trên sàn. " + Now);
            Assert.AreEqual(IDLE, State, "PlayerMovement không báo Idle khi đứng trên sàn. " + Now);
            Assert.IsTrue(AnimIs("Idle"), "Đứng yên trên sàn nhưng animator vẫn kẹt ở " + AnimName() + ". " + Now);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TC_ST_15_DangChet_BamD_KhongDiChuyenNgang()
        {
            yield return EnterLevel1();
            Floor(ArenaX, ArenaY);
            yield return PlaceOnFloor(ArenaX);

            GameObject trap = G.SpawnTrapOn(player);
            yield return WaitFor(() => !G.ColliderOn(player), 2f);
            Assert.IsFalse(G.ColliderOn(player), "Chạm bẫy nhưng Player không chết");
            Object.Destroy(trap);
            yield return WaitAnim("Death", 1f);

            float x0 = player.transform.position.x;
            bool alwaysDeath = true;
            yield return HoldD();
            yield return Sample(0.5f, () => alwaysDeath &= AnimIs("Death"));
            float dx = player.transform.position.x - x0;
            string now = Now;
            yield return LetGoD();

            Assert.IsTrue(alwaysDeath, "Rời animation Death khi bấm D. " + now);
            Assert.Less(Mathf.Abs(dx), 0.2f, $"Đang chết nhưng bấm D vẫn di chuyển ngang {dx:F2} đơn vị. {now}");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TC_ST_16_PauseKhiDangNhay_DungYen_BoQuaSpace_TiepTucVaTiepDat()
        {
            yield return EnterLevel1();
            Floor(ArenaX, ArenaY);
            yield return PlaceOnFloor(ArenaX);

            yield return PressJump();
            yield return WaitFor(() => AnimIs("Jump") && rb.linearVelocity.y > 3f, 1f);
            Assert.IsTrue(AnimIs("Jump"), Now);

            yield return PressP();
            Assert.AreEqual(0f, Time.timeScale, "Bấm P nhưng game không dừng");
            Vector2 pos = player.transform.position, vel = rb.linearVelocity;
            string animName = AnimName();

            yield return Tap(keyboard.spaceKey);
            yield return Sample(0.5f, () => { });
            Assert.Less(((Vector2)player.transform.position - pos).magnitude, 0.001f, "Đang Pause nhưng Player vẫn di chuyển");
            Assert.Less((rb.linearVelocity - vel).magnitude, 0.001f, "Đang Pause nhưng vận tốc thay đổi (Space có tác dụng?)");
            Assert.IsTrue(AnimIs(animName), $"Đang Pause nhưng animator đổi từ {animName} sang {AnimName()}");

            yield return PressP();
            Assert.AreEqual(1f, Time.timeScale, "Bấm P lần nữa nhưng game không chạy tiếp");
            yield return FixedSteps(1);
            Assert.Less(rb.linearVelocity.y, vel.y + 0.01f, "Space bấm trong lúc Pause vẫn làm Player nhảy sau khi tiếp tục. " + Now);

            yield return WaitFor(() => OnFloor(ArenaY) && AnimIs("Idle"), 4f);
            Assert.IsTrue(OnFloor(ArenaY) && AnimIs("Idle") && State == IDLE, "Tiếp tục sau Pause nhưng không tiếp đất về Idle. " + Now);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TC_ST_17_GameOver_BamPHaiLan_GameKhongDuocChayTiep()
        {
            yield return EnterLevel1(lives: 1);
            yield return G.KillPlayer(player);
            Component gameOver = G.Find("GameOverMenu");
            Assert.AreEqual(0f, Time.timeScale, "Hết mạng nhưng game không dừng");
            Assert.IsTrue(G.CanvasEnabled(gameOver, "gameOverCanvas"), "Hết mạng nhưng không hiện Game Over");
            Vector2 pos = player.transform.position;

            yield return PressP();
            yield return PressP();
            yield return Sample(0.5f, () => { });

            float moved = ((Vector2)player.transform.position - pos).magnitude;
            Assert.IsTrue(G.CanvasEnabled(gameOver, "gameOverCanvas"), "Menu Game Over bị đóng sau khi bấm P");
            Assert.AreEqual(0f, Time.timeScale,
                $"Đang ở Game Over, bấm P 2 lần thì game chạy tiếp (timeScale={Time.timeScale}); Player đã chết rơi thêm {moved:F2} đơn vị");
            Assert.Less(moved, 0.001f, "Đang ở Game Over nhưng Player vẫn di chuyển");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TC_ST_18_DungYen2Giay_LuonIdle_KhongTroi()
        {
            yield return EnterLevel1();
            Floor(ArenaX, ArenaY);
            yield return PlaceOnFloor(ArenaX);
            Vector2 pos = player.transform.position;

            var bad = new List<string>();
            yield return Sample(2f, () =>
            {
                if (!AnimIs("Idle") || State != IDLE || Mathf.Abs(rb.linearVelocity.y) > 0.1f) bad.Add(Now);
            });
            Assert.IsEmpty(bad, $"Đứng yên nhưng {bad.Count} frame không ở Idle, ví dụ: " + (bad.Count > 0 ? bad[0] : ""));
            Assert.Less(((Vector2)player.transform.position - pos).magnitude, 0.01f, "Đứng yên nhưng Player bị trôi");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TC_ST_19_VirtualGuy_ApTuongTrenKhong_KhongVaoWallSliding()
        {
            yield return EnterLevel1(VirtualGuy);
            Floor(ArenaX, ArenaY);
            WallAt(ArenaX + 2.5f);
            G.Teleport(player, new Vector2(ArenaX + 1.2f, ArenaY + 5f));
            yield return null;
            yield return HoldD();

            int wallFrames = 0;
            float maxRight = float.MinValue, t = 0f;
            while (!OnFloor(ArenaY) && t < 3f)
            {
                if (State == WALL || AnimIs("WallSliding")) wallFrames++;
                maxRight = Mathf.Max(maxRight, box.bounds.max.x);
                t += Time.deltaTime;
                yield return null;
            }
            yield return LetGoD();

            Assert.AreEqual(0, wallFrames, $"VirtualGuy không có wall grab nhưng vào WallSliding {wallFrames} frame");
            Assert.LessOrEqual(maxRight, ArenaX + 2.5f + 0.05f, "Player xuyên vào trong tường");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TC_ST_20_HoiSinh_ResetVanToc_KhongNhayFall()
        {
            yield return EnterLevel1();
            GameObject trap = G.SpawnTrapOn(player);
            yield return WaitFor(() => !G.ColliderOn(player), 2f);
            Assert.IsFalse(G.ColliderOn(player), "Chạm bẫy nhưng Player không chết");
            Object.Destroy(trap);

            // Trong 1,83 s chết, collider tắt nên Player rơi tự do; Respawn() phải đưa về trạng thái đứng yên
            yield return WaitFor(() => G.ColliderOn(player), 5f);
            Assert.IsTrue(G.ColliderOn(player), "Không hồi sinh. " + Now);
            float vyAtRespawn = rb.linearVelocity.y;
            var seen = new List<string>();
            yield return Sample(0.5f, () =>
            {
                string a = AnimName();
                if (seen.Count == 0 || seen[seen.Count - 1] != a) seen.Add(a);
            });
            string seq = string.Join(" -> ", seen);
            Assert.IsTrue(AnimIs("Idle") && State == IDLE, "Hồi sinh xong không đứng yên ở Idle. " + Now);
            Assert.Less(Mathf.Abs(vyAtRespawn), 1f,
                $"Respawn() không reset vận tốc: lúc hồi sinh v.y = {vyAtRespawn:F2} (còn giữ vận tốc rơi lúc chết); animator: {seq}");
            Assert.IsFalse(seen.Contains("Fall"), $"Vừa hồi sinh trên mặt đất nhưng animator nháy sang Fall: {seq}");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator TC_ST_21_HoiSinhChuaQuaCheckpoint_TaiDiemBatDauMan()
        {
            yield return EnterLevel1();
            Vector3 levelStart = player.transform.position;
            Vector3 spawn = (Vector3)G.Get(G.Comp(player, "PlayerLife"), "initSpawnPos");

            yield return G.KillPlayer(player);
            Assert.IsTrue(G.ColliderOn(player), "Không hồi sinh. " + Now);
            yield return WaitAnimStable("Idle", 0.5f, 3f);
            Assert.Less(Mathf.Abs(player.transform.position.x - levelStart.x), 1f,
                $"Chưa qua checkpoint nhưng hồi sinh ở x={player.transform.position.x:F2} (initSpawnPos = ({spawn.x:F2}, {spawn.y:F2})), " +
                $"cách điểm bắt đầu màn x={levelStart.x:F2} tới {Mathf.Abs(player.transform.position.x - levelStart.x):F1} đơn vị");
        }
    }
}
