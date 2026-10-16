// ModAnimationTests.cs
// Feature: a mod model's glTF animations loop like the game's own (ModGltf.LoopAnimations, called by ModShipBuilder.Build for a
// ship's hull and a kit's parts): glTFast puts them on a legacy Animation with the first clip at the default wrap mode, which
// played it once and stopped.

using GoF2Remake.Modding;
using NUnit.Framework;
using UnityEngine;

namespace GoF2Remake.Tests
{
    public class ModAnimationTests
    {
        [Test]
        public void GltfAnimationsLoopAndPlayByThemselves()
        {
            var root = new GameObject("scene");
            var pod = new GameObject("pod");
            pod.transform.SetParent(root.transform, false);
            var clip = new AnimationClip { legacy = true, name = "bob" };
            clip.SetCurve("pod", typeof(Transform), "localPosition.y", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            var anim = root.AddComponent<Animation>();   // as glTFast's GameObjectInstantiator adds it
            anim.AddClip(clip, clip.name);
            anim.clip = clip;
            anim.playAutomatically = false;
            try
            {
                ModGltf.LoopAnimations(root);
                Assert.AreEqual(WrapMode.Loop, anim.wrapMode);
                Assert.AreEqual(WrapMode.Loop, clip.wrapMode);
                Assert.IsTrue(anim.playAutomatically);
                Assert.AreEqual(AnimationCullingType.AlwaysAnimate, anim.cullingType, "plays off screen too, like PartAnimation");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void AModelWithoutAnimationsIsLeftAlone()
        {
            var root = new GameObject("scene");
            try { Assert.DoesNotThrow(() => ModGltf.LoopAnimations(root)); }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
