using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Iridescent.Timeline
{
    /// <summary>
    /// A Track whose clips control time-related elements on a GameObject.
    /// </summary>
    [TrackClipType(typeof(CustomControlPlayableAsset), false)]
    [ExcludeFromPreset]
    [TrackColor(0.89f, 0.84f, 0.98f)]
    public class CustomControlTrack : TrackAsset
    {
#if UNITY_EDITOR
        private static readonly HashSet<PlayableDirector> s_ProcessedDirectors = new HashSet<PlayableDirector>();

        /// <inheritdoc/>
        public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
        {
            if (director == null)
                return;

            // avoid recursion
            if (s_ProcessedDirectors.Contains(director))
                return;

            s_ProcessedDirectors.Add(director);

            var particlesToPreview = new HashSet<ParticleSystem>();
            var activationToPreview = new HashSet<GameObject>();
            var timeControlToPreview = new HashSet<MonoBehaviour>();
            var subDirectorsToPreview = new HashSet<PlayableDirector>();

            foreach (var clip in GetClips())
            {
                var controlPlayableAsset = clip.asset as CustomControlPlayableAsset;
                if (controlPlayableAsset == null)
                    continue;

                var gameObject = controlPlayableAsset.sourceGameObject.Resolve(director);
                if (gameObject == null)
                    continue;

                if (controlPlayableAsset.updateParticle)
                    particlesToPreview.UnionWith(gameObject.GetComponentsInChildren<ParticleSystem>(true));
                if (controlPlayableAsset.updateITimeControl)
                    timeControlToPreview.UnionWith(CustomControlPlayableAsset.GetControlableScripts(gameObject));
                if (controlPlayableAsset.updateDirector)
                    subDirectorsToPreview.UnionWith(controlPlayableAsset.GetComponent<PlayableDirector>(gameObject));
            }

            CustomControlPlayableAsset.PreviewParticles(driver, particlesToPreview);
            CustomControlPlayableAsset.PreviewActivation(driver, activationToPreview);
            CustomControlPlayableAsset.PreviewTimeControl(driver, director, timeControlToPreview);
            CustomControlPlayableAsset.PreviewDirectors(driver, subDirectorsToPreview);

            s_ProcessedDirectors.Remove(director);

            particlesToPreview.Clear();
            activationToPreview.Clear();
            timeControlToPreview.Clear();
            subDirectorsToPreview.Clear();
        }

#endif
    }
}