using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEditor.Timeline;

namespace Iridescent.Timeline
{
    [CustomEditor(typeof(CustomControlPlayableAsset)), CanEditMultipleObjects]
    class CustomControlPlayableInspector : Editor
    {
        static class Styles
        {
            static string s_DisabledBecauseOfSelfControlTooltip = "Must be disabled when the Source Game Object references the same PlayableDirector component that is being controlled";
            public static readonly GUIContent activationContent = L10n.TextContent("Control Activation", "When checked the clip will control the active state of the source game object");
            public static readonly GUIContent activationDisabledContent = L10n.TextContent("Control Activation", s_DisabledBecauseOfSelfControlTooltip);
            public static readonly GUIContent prefabContent = L10n.TextContent("Prefab", "A prefab to instantiate as a child object of the source game object");
            public static readonly GUIContent advancedContent = L10n.TextContent("Advanced");
            public static readonly GUIContent updateParticleSystemsContent = L10n.TextContent("Control Particle Systems", "Synchronize the time between the clip and any particle systems on the game object");
            public static readonly GUIContent updatePlayableDirectorContent = L10n.TextContent("Control Playable Directors", "Synchronize the time between the clip and any playable directors on the game object");
            public static readonly GUIContent updatePlayableDirectorDisabledContent = L10n.TextContent("Control Playable Directors", s_DisabledBecauseOfSelfControlTooltip);
            public static readonly GUIContent stopOnPauseEventContent = L10n.TextContent("Stop on Pause Event", "False to prevent flicker from occurring, true if there is a problem with that condition.");
            public static readonly GUIContent stopOnPauseEventDisabledContent = L10n.TextContent("Stop on Pause Event", s_DisabledBecauseOfSelfControlTooltip);
            public static readonly GUIContent updateITimeControlContent = L10n.TextContent("Control ITimeControl", "Synchronize the time between the clip and any Script that implements the ITimeControl interface on the game object");
            public static readonly GUIContent updateHierarchy = L10n.TextContent("Control Children", "Search child game objects for particle systems and playable directors");
            public static readonly GUIContent randomSeedContent = L10n.TextContent("Random Seed", "A random seed to provide the particle systems for consistent previews. This will only be used on particle systems where AutoRandomSeed is on.");
            public static readonly GUIContent postPlayableContent = L10n.TextContent("Post Playback", "The active state to the leave the game object when the timeline is finished. \n\nRevert will leave the game object in the state it was prior to the timeline being run");
        }

        SerializedProperty m_SourceObject;
        SerializedProperty m_PrefabObject;
        SerializedProperty m_UpdateParticle;
        SerializedProperty m_UpdateDirector;
        private SerializedProperty m_StopOnPauseEvent;
        SerializedProperty m_UpdateITimeControl;
        SerializedProperty m_SearchHierarchy;
        SerializedProperty m_UseActivation;
        SerializedProperty m_PostPlayback;
        SerializedProperty m_RandomSeed;
        bool m_CycleReference;


        GUIContent m_SourceObjectLabel = new GUIContent();

        // the director that the selection was made with. Normally this matches the active director in timeline,
        //  but persists if the active timeline changes (case 962516)
        private PlayableDirector contextDirector
        {
            get
            {
                if (serializedObject == null)
                    return null;
                return serializedObject.context as PlayableDirector;
            }
        }

        public void OnEnable()
        {
            if (target == null) // case 946080
                return;

            m_SourceObject = serializedObject.FindProperty("sourceGameObject");
            m_PrefabObject = serializedObject.FindProperty("prefabGameObject");

            m_UpdateParticle = serializedObject.FindProperty("updateParticle");
            m_UpdateDirector = serializedObject.FindProperty("updateDirector");
            m_StopOnPauseEvent = serializedObject.FindProperty("stopOnPauseEvent");
            m_UpdateITimeControl = serializedObject.FindProperty("updateITimeControl");
            m_SearchHierarchy = serializedObject.FindProperty("searchHierarchy");
            m_UseActivation = serializedObject.FindProperty("active");
            m_PostPlayback = serializedObject.FindProperty("postPlayback");
            m_RandomSeed = serializedObject.FindProperty("particleRandomSeed");
            CheckForCyclicReference();
        }

        public override void OnInspectorGUI()
        {
            if (target == null)
                return;

            serializedObject.Update();

            m_SourceObjectLabel.text = m_SourceObject.displayName;

            if (m_PrefabObject.objectReferenceValue != null)
                m_SourceObjectLabel.text = L10n.Tr("Parent Object");

            bool selfControlled = false;


            EditorGUI.BeginChangeCheck();

            // リフレクション
            var tGUIMixedValueScope = Type.GetType("UnityEditor.GUIMixedValueScope,Unity.Timeline.Editor");
            var guiMixedValueScopeConstructor = tGUIMixedValueScope.GetConstructor(new Type[] {typeof(bool)});
            using ((IDisposable)guiMixedValueScopeConstructor.Invoke(new object[]{m_SourceObject.hasMultipleDifferentValues}))
                EditorGUILayout.PropertyField(m_SourceObject, m_SourceObjectLabel);

            // リフレクション
            var tTimelineWindow = Type.GetType("UnityEditor.Timeline.TimelineWindow,Unity.Timeline.Editor");
            var timelineWindowInstanceInfo = tTimelineWindow.GetProperty("instance");
            var timelineWindowInstance = timelineWindowInstanceInfo.GetValue(null);
            var timelineWindowInstanceStateInfo = tTimelineWindow.GetProperty("state");
            var timelineWindowInstanceState = timelineWindowInstanceStateInfo.GetValue(timelineWindowInstance);
            var sourceGameObject = m_SourceObject.exposedReferenceValue as GameObject;
            
            selfControlled = m_PrefabObject.objectReferenceValue == null && timelineWindowInstance != null && timelineWindowInstanceState != null &&
                contextDirector != null && sourceGameObject == contextDirector.gameObject;

            if (EditorGUI.EndChangeCheck())
            {
                CheckForCyclicReference();
                if (!selfControlled)
                    DisablePlayOnAwake(sourceGameObject);
            }

            if (selfControlled)
            {
                EditorGUILayout.HelpBox(L10n.Tr("The assigned GameObject references the same PlayableDirector component being controlled."), MessageType.Warning);
            }
            else if (m_CycleReference)
            {
                EditorGUILayout.HelpBox(L10n.Tr("The assigned GameObject contains a PlayableDirector component that results in a circular reference."), MessageType.Warning);
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(m_PrefabObject, Styles.prefabContent);
            EditorGUI.indentLevel--;

            using (new EditorGUI.DisabledScope(selfControlled))
            {
                EditorGUILayout.PropertyField(m_UseActivation, selfControlled ? Styles.activationDisabledContent : Styles.activationContent);
                if (m_UseActivation.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(m_PostPlayback, Styles.postPlayableContent);
                    EditorGUI.indentLevel--;
                }
            }

            m_SourceObject.isExpanded = EditorGUILayout.Foldout(m_SourceObject.isExpanded, Styles.advancedContent, true);

            if (m_SourceObject.isExpanded)
            {
                EditorGUI.indentLevel++;

                using (new EditorGUI.DisabledScope(selfControlled && !m_SearchHierarchy.boolValue))
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(m_UpdateDirector, selfControlled ? Styles.updatePlayableDirectorDisabledContent : Styles.updatePlayableDirectorContent);
                    if (m_UpdateDirector.boolValue)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(m_StopOnPauseEvent,
                            selfControlled ? Styles.stopOnPauseEventDisabledContent : Styles.stopOnPauseEventContent);
                        EditorGUI.indentLevel--;
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        CheckForCyclicReference();
                    }
                }

                EditorGUILayout.PropertyField(m_UpdateParticle, Styles.updateParticleSystemsContent);
                if (m_UpdateParticle.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(m_RandomSeed, Styles.randomSeedContent);
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.PropertyField(m_UpdateITimeControl, Styles.updateITimeControlContent);

                EditorGUILayout.PropertyField(m_SearchHierarchy, Styles.updateHierarchy);

                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        //
        // Fix for a workflow issue where scene objects with directors have play on awake by default enabled.
        //  This causes confusion when the director is played within another director, so we disable it on assignment
        //  to avoid the issue, but not force the issue on the user
        public void DisablePlayOnAwake(GameObject sourceObject)
        {
            if (sourceObject != null && m_UpdateDirector.boolValue)
            {
                if (m_SearchHierarchy.boolValue)
                {
                    var directors = sourceObject.GetComponentsInChildren<PlayableDirector>();
                    foreach (var d in directors)
                    {
                        DisablePlayOnAwake(d);
                    }
                }
                else
                {
                    DisablePlayOnAwake(sourceObject.GetComponent<PlayableDirector>());
                }
            }
        }

        public void DisablePlayOnAwake(PlayableDirector director)
        {
            if (director == null)
                return;
            var obj = new SerializedObject(director);
            var prop = obj.FindProperty("m_InitialState");
            prop.enumValueIndex = (int)PlayState.Paused;
            obj.ApplyModifiedProperties();
        }

        void CheckForCyclicReference()
        {
            serializedObject.ApplyModifiedProperties();
            m_CycleReference = false;

            PlayableDirector director = contextDirector;
            if (contextDirector == null)
                return;

            var tControlPlayableUtility = Type.GetType("UnityEditor.Timeline.ControlPlayableUtility,Unity.Timeline.Editor");
            var detectCycleInfo = tControlPlayableUtility.GetMethod("DetectCycle", BindingFlags.Static | BindingFlags.Public);
            foreach (var asset in targets.OfType<ControlPlayableAsset>())
            {
                if ((bool)detectCycleInfo.Invoke(null, new object[]{asset, director}))
                {
                    m_CycleReference = true;
                    return;
                }
            }
        }
    }
}
