// PhotoModeTests.cs
// Feature: the photo tools of Action Freeze and Inspect ship (UI.PhotoTools, players' suggestion "Everspace-style Action
// Freeze", 2026-10-17): the free camera (World.FreeCamera) flies anywhere within reach of the ship and above the floor; the
// frame cuts the screenshot (PhotoSettings.FrameRect); Reset; the hangar orbit takes the free pose, roll and field of view
// and keeps them through the blend back (World.HangarInspect).

using GoF2Remake.UI;
using GoF2Remake.World;
using NUnit.Framework;
using UnityEngine;

namespace GoF2Remake.Tests
{
    public class PhotoModeTests
    {
        [Test]
        public void TheFreeCameraStartsWhereTheCameraIsAndLooksTheSameWay()
        {
            var cam = new FreeCamera();
            var rot = Quaternion.LookRotation(new Vector3(1f, 0.5f, 1f));
            cam.Begin(new Vector3(10f, 2f, 3f), rot, Vector3.zero, 100f);
            Assert.That(Vector3.Distance(cam.Position, new Vector3(10f, 2f, 3f)), Is.LessThan(1e-4f));
            Assert.That(Vector3.Angle(cam.Forward, rot * Vector3.forward), Is.LessThan(0.01f));
        }

        [Test]
        public void ItMovesInItsOwnFrameAndUpAlongTheWorlds()
        {
            var cam = new FreeCamera();
            cam.Begin(Vector3.zero, Quaternion.LookRotation(Vector3.right), Vector3.zero, 1000f);   // facing +x
            cam.Move(new Vector3(0f, 0f, 1f), 10f);
            Assert.That(Vector3.Distance(cam.Position, new Vector3(10f, 0f, 0f)), Is.LessThan(1e-3f), "forward");
            cam.Move(new Vector3(1f, 0f, 0f), 5f);
            Assert.That(Vector3.Distance(cam.Position, new Vector3(10f, 0f, -5f)), Is.LessThan(1e-3f), "right of +x is -z");
            cam.Move(new Vector3(0f, 1f, 0f), 3f);
            Assert.AreEqual(3f, cam.Position.y, 1e-3f, "up along the world's up");
        }

        [Test]
        public void ItStaysInReachOfTheShipAndAboveTheFloor()
        {
            var cam = new FreeCamera();
            cam.Begin(new Vector3(0f, 5f, 0f), Quaternion.identity, Vector3.zero, 50f, floor: 2f);
            cam.Move(new Vector3(0f, 0f, 1f), 500f);
            Assert.That(cam.Position.magnitude, Is.LessThanOrEqualTo(50.001f));
            cam.Move(new Vector3(0f, -1f, 0f), 500f);
            Assert.That(cam.Position.y, Is.GreaterThanOrEqualTo(2f));
        }

        [Test]
        public void ItNeverLooksStraightUpOrDown()
        {
            var cam = new FreeCamera();
            cam.Begin(Vector3.zero, Quaternion.identity, Vector3.zero, 10f);
            cam.Look(0f, 10f);
            Assert.AreEqual(FreeCamera.MaxPitch, cam.Pitch, 1e-5f);
            cam.Look(0f, -20f);
            Assert.AreEqual(-FreeCamera.MaxPitch, cam.Pitch, 1e-5f);
        }

        [TestCase(1920, 1080, 0f, 0, 0, 1920, 1080)]
        [TestCase(1920, 1080, 1f, 420, 0, 1080, 1080)]          // 1:1 in a wide picture: the middle square
        [TestCase(1920, 1080, 21f / 9f, 0, 128, 1920, 823)]     // wider than the picture: bars above and below
        [TestCase(1920, 1080, 9f / 16f, 656, 0, 608, 1080)]     // portrait
        public void TheFrameCutsTheMiddleOfThePicture(int w, int h, float aspect, int x, int y, int fw, int fh)
        {
            var r = PhotoSettings.FrameRect(w, h, aspect);
            Assert.AreEqual(new RectInt(x, y, fw, fh), r);
        }

        [Test]
        public void ResetPutsTheLookBack()
        {
            PhotoSettings.DefaultFov = 50f;
            PhotoSettings.Fov = 20f;
            PhotoSettings.Roll = 30f;
            PhotoSettings.Vignette = 0.5f;
            PhotoSettings.Dof = PhotoSettings.DofMode.Manual;
            PhotoSettings.Frame = 3;
            PhotoSettings.Reset();
            Assert.AreEqual(50f, PhotoSettings.Fov);
            Assert.AreEqual(0f, PhotoSettings.Roll);
            Assert.AreEqual(0f, PhotoSettings.Vignette);
            Assert.AreEqual(PhotoSettings.DofMode.Game, PhotoSettings.Dof);
            Assert.AreEqual(0, PhotoSettings.Frame);
        }

        [Test]
        public void TheFocusDistanceRunsFromAMetreToThreeKilometres()
        {
            float before = PhotoSettings.Focus;
            try
            {
                PhotoSettings.Focus = 0f;
                Assert.AreEqual(1f, PhotoSettings.FocusMetres, 1e-3f);
                PhotoSettings.Focus = 1f;
                Assert.AreEqual(PhotoSettings.MaxFocusMetres, PhotoSettings.FocusMetres, 0.5f);
            }
            finally { PhotoSettings.Focus = before; }
        }

        [Test]
        public void APressThatBeganOnTheUiNeverTurnsTheCamera()
        {
            // A slider dragged off the settings panel: the press began on it, so the camera ignores it until release.
            var tools = new PhotoTools(new UnityEngine.UIElements.VisualElement(), null);
            bool overUi = true;
            System.Func<Vector2, bool> ui = _ => overUi;
            Assert.IsTrue(tools.PressOverUi(new Vector2(10f, 10f), ui), "pressed on the slider");
            overUi = false;   // dragged off it
            Assert.IsTrue(tools.PressOverUi(new Vector2(900f, 10f), ui), "still the slider's press");
            Assert.IsFalse(tools.PressOverUi(null, ui), "released");
            Assert.IsFalse(tools.PressOverUi(new Vector2(900f, 10f), ui), "a new press on the view turns the camera");
            overUi = true;    // that press passing over the UI stays the camera's
            Assert.IsFalse(tools.PressOverUi(new Vector2(10f, 10f), ui));
        }

        [TestCase(1920, 1080, 0, 1)]
        [TestCase(1920, 1080, 1, 2)]
        [TestCase(1920, 1080, 2, 4)]
        [TestCase(3840, 2160, 2, 4)]      // 15360 px wide: within a texture's 16384
        [TestCase(5120, 1440, 2, 3)]      // a super-wide screen at x4 would pass it: x3
        public void TheScreenshotSizeStaysWithinATexture(int w, int h, int size, int expected)
        {
            int before = PhotoSettings.Size;
            try { PhotoSettings.Size = size; Assert.AreEqual(expected, PhotoSettings.SuperSize(w, h)); }
            finally { PhotoSettings.Size = before; }
        }

        [Test]
        public void TheHangarOrbitTakesTheFreePoseAndKeepsItThroughTheBlendBack()
        {
            var insp = new HangarInspect();
            insp.Begin(new Vector3(0f, 10f, -100f), new Bounds(Vector3.zero, new Vector3(20f, 6f, 30f)));
            insp.Tick(5000f);   // fully blended in
            var go = new GameObject("cam");
            try
            {
                var pose = new Vector3(5f, 3f, -12f);
                insp.SetFreePose(pose, Quaternion.identity);
                insp.RollDeg = 20f;
                insp.Apply(go.transform, Vector3.zero, Quaternion.identity, 1f);
                Assert.That(Vector3.Distance(go.transform.position, pose), Is.LessThan(1e-3f));
                Assert.AreEqual(20f, go.transform.eulerAngles.z, 0.01f, "the roll about the view");
                insp.End();
                insp.Tick(300f);   // part way back: still the free pose's
                Assert.IsTrue(insp.Free);
                insp.Tick(5000f);  // back at the hangar camera: gone
                Assert.IsFalse(insp.Free);
                Assert.AreEqual(0f, insp.RollDeg);
                Assert.AreEqual(0f, insp.FovDeg);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
