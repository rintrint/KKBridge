using KKBridge.Extensions;
using Studio;
using System;
using System.Reflection;
using System.Xml;

namespace KKBridge.Compatibility
{
    internal class TimelineCompatibility
    {
        private static Func<float> _getPlaybackTime;

        private static Func<float> _getDuration;

        private static Func<bool> _getIsPlaying;

        private static Action _play;

        private static MethodInfo _addInterpolableModelStatic;

        private static MethodInfo _addInterpolableModelDynamic;

        private static Action _refreshInterpolablesList;

        private static Type _interpolableDelegate;

        public static bool Init()
        {
            try
            {
                Type type = Type.GetType("Timeline.Timeline,Timeline");
                if (type != null)
                {
                    _getPlaybackTime = (Func<float>)Delegate.CreateDelegate(typeof(Func<float>), type.GetProperty("playbackTime", BindingFlags.Static | BindingFlags.Public).GetGetMethod());
                    _getDuration = (Func<float>)Delegate.CreateDelegate(typeof(Func<float>), type.GetProperty("duration", BindingFlags.Static | BindingFlags.Public).GetGetMethod());
                    _getIsPlaying = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), type.GetProperty("isPlaying", BindingFlags.Static | BindingFlags.Public).GetGetMethod());
                    _play = (Action)Delegate.CreateDelegate(typeof(Action), type.GetMethod("Play", BindingFlags.Static | BindingFlags.Public));
                    _addInterpolableModelStatic = type.GetMethod("AddInterpolableModelStatic", BindingFlags.Static | BindingFlags.Public);
                    _addInterpolableModelDynamic = type.GetMethod("AddInterpolableModelDynamic", BindingFlags.Static | BindingFlags.Public);
                    _refreshInterpolablesList = (Action)Delegate.CreateDelegate(typeof(Action), type.GetMethod("RefreshInterpolablesList", BindingFlags.Static | BindingFlags.Public));
                    _interpolableDelegate = Type.GetType("Timeline.InterpolableDelegate,Timeline");
                    return true;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("Exception caught when trying to find Timeline: " + ex);
            }
            return false;
        }

        public static float GetPlaybackTime()
        {
            return _getPlaybackTime();
        }

        public static float GetDuration()
        {
            return _getDuration();
        }

        public static bool GetIsPlaying()
        {
            return _getIsPlaying();
        }

        public static void Play()
        {
            _play();
        }

        public static void AddInterpolableModelStatic(string owner, string id, object parameter, string name, Action<ObjectCtrlInfo, object, object, object, float> interpolateBefore, Action<ObjectCtrlInfo, object, object, object, float> interpolateAfter, Func<ObjectCtrlInfo, bool> isCompatibleWithTarget, Func<ObjectCtrlInfo, object, object> getValue, Func<object, XmlNode, object> readValueFromXml, Action<object, XmlTextWriter, object> writeValueToXml, Func<ObjectCtrlInfo, XmlNode, object> readParameterFromXml = null, Action<ObjectCtrlInfo, XmlTextWriter, object> writeParameterToXml = null, Func<ObjectCtrlInfo, object, object, object, bool> checkIntegrity = null, bool useOciInHash = true, Func<string, ObjectCtrlInfo, object, string> getFinalName = null, Func<ObjectCtrlInfo, object, bool> shouldShow = null)
        {
            Delegate obj = null;
            if (interpolateBefore != null)
            {
                obj = Delegate.CreateDelegate(_interpolableDelegate, interpolateBefore.Target, interpolateBefore.Method);
            }
            Delegate obj2 = null;
            if (interpolateAfter != null)
            {
                obj2 = Delegate.CreateDelegate(_interpolableDelegate, interpolateAfter.Target, interpolateAfter.Method);
            }
            _addInterpolableModelStatic.Invoke(null, new object[16]
            {
            owner, id, parameter, name, obj, obj2, isCompatibleWithTarget, getValue, readValueFromXml, writeValueToXml,
            readParameterFromXml, writeParameterToXml, checkIntegrity, useOciInHash, getFinalName, shouldShow
            });
        }

        public static void AddInterpolableModelDynamic(string owner, string id, string name, Action<ObjectCtrlInfo, object, object, object, float> interpolateBefore, Action<ObjectCtrlInfo, object, object, object, float> interpolateAfter, Func<ObjectCtrlInfo, bool> isCompatibleWithTarget, Func<ObjectCtrlInfo, object, object> getValue, Func<object, XmlNode, object> readValueFromXml, Action<object, XmlTextWriter, object> writeValueToXml, Func<ObjectCtrlInfo, object> getParameter, Func<ObjectCtrlInfo, XmlNode, object> readParameterFromXml = null, Action<ObjectCtrlInfo, XmlTextWriter, object> writeParameterToXml = null, Func<ObjectCtrlInfo, object, object, object, bool> checkIntegrity = null, bool useOciInHash = true, Func<string, ObjectCtrlInfo, object, string> getFinalName = null, Func<ObjectCtrlInfo, object, bool> shouldShow = null)
        {
            Delegate obj = null;
            if (interpolateBefore != null)
            {
                obj = Delegate.CreateDelegate(_interpolableDelegate, interpolateBefore.Target, interpolateBefore.Method);
            }
            Delegate obj2 = null;
            if (interpolateAfter != null)
            {
                obj2 = Delegate.CreateDelegate(_interpolableDelegate, interpolateAfter.Target, interpolateAfter.Method);
            }
            _addInterpolableModelDynamic.Invoke(null, new object[16]
            {
            owner, id, name, obj, obj2, isCompatibleWithTarget, getValue, readValueFromXml, writeValueToXml, getParameter,
            readParameterFromXml, writeParameterToXml, checkIntegrity, useOciInHash, getFinalName, shouldShow
            });
        }

        public static void RefreshInterpolablesList()
        {
            if (_refreshInterpolablesList != null)
            {
                _refreshInterpolablesList();
            }
        }
    }
}
