﻿using UnityEngine;
using System.Collections;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Threading;
using UnityEngine.SceneManagement;

[Serializable]
public class ControllableData
{
    public string dataID;

    public List<string> nameList;
    public List<string> valueList;

    public ControllableData()
    {
        nameList = new List<string>();
        valueList = new List<string>();
    }
}

public class Controllable : MonoBehaviour
{
    public object TargetScript;

    [Header("Controllable")]
    public string id;

    [HideInInspector]
    public string folder = "";
    public bool debug = false;
    [HideInInspector]
    public string targetDirectory;
    [HideInInspector]
    public string sourceScene;
    [HideInInspector]
    public bool usePanel = true, usePresets = false;

    public Dictionary<string, MemberInfo> Properties;
    public Dictionary<string, MemberInfo> TargetProperties;
    public List<object> PreviousPropertiesValues;

    public Dictionary<string, MethodInfo> Methods;

    public delegate void UIValueChangedEvent(string name);
    public event UIValueChangedEvent uiValueChanged;

    public delegate void ControllableValueChangedEvent(string name);
    public event ControllableValueChangedEvent controllableValueChanged;

    public delegate void ScriptValueChangedEvent(string name);
    public event ScriptValueChangedEvent scriptValueChanged;

    public delegate void ControllableNameChangedEvent(Controllable c, string oldName);
    public event ControllableNameChangedEvent controllableNameChanged;

    [OSCProperty(TargetList = "presetList", IncludeInPresets = false)] public string currentPreset;

    public List<string> presetList;

    private string tempFileName = "_temp.pst";

    public virtual void Awake()
    {
        this.scriptValueChanged += OnScriptValueChanged;
        this.uiValueChanged += OnUiValueChanged;

        //PROPERTIES
        Properties = new Dictionary<string, MemberInfo>();
        TargetProperties = new Dictionary<string, MemberInfo>();
        PreviousPropertiesValues = new List<object>();

        MemberInfo[] objectProperties = GetType().GetMembers(BindingFlags.GetProperty | BindingFlags.GetField | BindingFlags.Instance | BindingFlags.Public);
        MemberInfo[] scriptProperties = objectProperties;


        if (TargetScript == null) TargetScript = this;
        else
        {
            objectProperties = TargetScript.GetType().GetMembers(BindingFlags.GetProperty | BindingFlags.GetField | BindingFlags.Instance | BindingFlags.Public);
        }

        Debug.Log("Found " + objectProperties.Length + " fields and properties in " + id);

        for (int i = 0; i < objectProperties.Length; i++)
        {
            MemberInfo info = objectProperties[i];
            OSCProperty attribute = Attribute.GetCustomAttribute(info, typeof(OSCProperty)) as OSCProperty;
            if (attribute != null)
            {
                if (info.Name == "currentPreset" && !usePresets) continue;

                String propName = attribute.customName == null ? info.Name : attribute.customName;

                Properties.Add(propName, info);
                
                for (int j = 0; j < scriptProperties.Length; j++)
                {
                    if (scriptProperties[j].Name == info.Name)
                    {
                        TargetProperties.Add(propName, scriptProperties[j]);
                        break;
                    }
                }

                PreviousPropertiesValues.Add(getInfoValue(info));
            }
        }

        //METHODS

        Methods = new Dictionary<string, MethodInfo>();

        MethodInfo[] methodFields = GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);

        for (int i = 0; i < methodFields.Length; i++)
        {
            MethodInfo info = methodFields[i];
            OSCMethod attribute = Attribute.GetCustomAttribute(info, typeof(OSCMethod)) as OSCMethod;
            if (attribute != null)
            {
                if ((info.Name == "Save" || info.Name == "SaveAs" || info.Name == "Load" || info.Name == "Show") && !usePresets) continue;
                Methods.Add(info.Name, info);
            }
        }

        if (string.IsNullOrEmpty(id)) id = TargetScript.GetType().Name;

        sourceScene = SceneManager.GetActiveScene().name;
    }

    public virtual void OnScriptValueChanged(string name)
    {
        MemberInfo[] iList = TargetScript.GetType().GetMember(name);
        if(iList.Length == 0)
        {
            if (debug) Debug.Log("Name : " + name + " is null in target");
        }
        else if (iList[0] is PropertyInfo || iList[0] is FieldInfo)
        {
            MemberInfo[] thisList = GetType().GetMember(name);
            if(thisList.Length > 0) setInfoValue(thisList[0], getScriptInfoValue(iList[0]));
        }
    }

    public virtual void OnUiValueChanged(string name)
    {
        MemberInfo[] iList = TargetScript.GetType().GetMember(name);
        if (iList.Length == 0)
        {
            if (debug)
                Debug.Log("Name : " + name + " is null in target");
            return;
        }

        MemberInfo[] thisList = GetType().GetMember(name);
        if (thisList.Length > 0) setInfoValue(thisList[0], getScriptInfoValue(iList[0]));
    }


    public virtual void setId(string newId)
    {
        string oldId = id;
        id = newId;
        if (controllableNameChanged != null) controllableNameChanged(this, oldId);
    }

    public virtual void OnEnable()
    {
        if (debug)
            Debug.Log("Registering " + this.GetType().Name + " script on " + this.gameObject.name);
        ControllableMaster.Register(this);

        if (usePresets)
        {
            presetList = new List<string>();
            ReadFileList();

            if (presetList.Count >= 1)
            {
                currentPreset = presetList[0];
                LoadLatestUsedPreset();
            }
        }
    }


    public virtual void Update() //Warn UI if attribut changes
    {
        var propertiesArray = TargetProperties.Values.ToArray();


        for (var i = 0; i < TargetProperties.Count; i++)
        {
            var value = getScriptInfoValue(propertiesArray[i]);
            if (value.ToString() != PreviousPropertiesValues[i].ToString())
            {
                // Debug.Log("Difference between " + propertiesArray[i].GetValue(this) + " and " + PreviousPropertiesValues[i].ToString());
                if (scriptValueChanged != null) scriptValueChanged(propertiesArray[i].Name);
                RaiseEventValueChanged(propertiesArray[i].Name);
                PreviousPropertiesValues[i] = value;
            }
        }
    }

    public void LoadLatestUsedPreset()
    {
        //Check if the temp preset containing the last used preset exists
        if (!File.Exists(targetDirectory + tempFileName)) return;

        var file = new StreamReader(targetDirectory + tempFileName);

        var lastPresetRead = file.ReadLine();
        file.Close();

        if (debug)
            Debug.Log("LastUsedPreset for " + id + " : " + lastPresetRead);

        if (string.IsNullOrEmpty(lastPresetRead)) return;

        currentPreset = lastPresetRead;
        Load();

        if (scriptValueChanged != null) scriptValueChanged("currentPreset");
        //if (uiValueChanged != null) uiValueChanged("currentPreset");
        RaiseEventValueChanged("currentPreset");
    }

    public void ReadFileList()
    {
        presetList.Clear();
        targetDirectory = "Presets/" + (folder.Length > 0 ? folder : sourceScene) + "/" + id + "/";
        Directory.CreateDirectory(targetDirectory);
        foreach (var t in Directory.GetFiles(targetDirectory))
        {
            var onlyFileName = t.Split('/').Last();
            //Don't put temp file in list
            if (onlyFileName == tempFileName) continue;
            presetList.Add(onlyFileName);
        }

        if (scriptValueChanged != null) scriptValueChanged("currentPreset");
        RaiseEventValueChanged("currentPreset");
    }

    [OSCMethod]
    public void Save()
    {
        if (string.IsNullOrEmpty(currentPreset))
        {
            SaveAs();
            return;
        }

        Save(currentPreset);
    }

    [OSCMethod]
    public void SaveAs()
    {

        var date = DateTime.Today.Day + "-" + DateTime.Today.Month + "-" + DateTime.Today.Year + "_" +
                   DateTime.Now.Hour + "-" + DateTime.Now.Minute + "-" + DateTime.Now.Second;
        var fileName = date + ".pst";

        Save(fileName);
    }

    private void Save(string fileName)
    {
        targetDirectory = "Presets/" + (folder.Length > 0 ? folder : sourceScene) + "/" + id + "/";
        if (debug)
            Debug.Log("Saving in " + targetDirectory + fileName + "...");
        //create file
        if (!Directory.Exists(targetDirectory)) Directory.CreateDirectory(targetDirectory);
        var file = File.OpenWrite(targetDirectory + fileName);
        file.Close();

        CallMeBeforeSave();
        File.WriteAllText(targetDirectory + fileName, JsonUtility.ToJson(this.getData()));

        if (debug)
            Debug.Log("Saved in " + targetDirectory + fileName);

        currentPreset = fileName;
        ReadFileList();
    }

    [OSCMethod]
    public void Load()
    {
        LoadWithName(currentPreset);
    }

    [OSCMethod]
    public void Show() //Show preset file in explorer
    {
        if (currentPreset == "") return;

        var itemPath = targetDirectory + currentPreset;
        itemPath = itemPath.Replace(@"/", @"\");   // explorer doesn't like front slashes
        System.Diagnostics.Process.Start("explorer.exe", "/select," + itemPath);
    }

    [OSCMethod]
    public void LoadWithName(string fileName, float duration = 0, string tweenStyle = null)
    {
        if (!fileName.EndsWith(".pst"))
            fileName += ".pst";

        if (debug)
            Debug.Log("Loading " + fileName + " preset for " + id + " with " + (tweenStyle == null ? " no tween " : tweenStyle));

        StreamReader file;
        try
        {
            file = new StreamReader(targetDirectory + fileName);
            ControllableData cData = JsonUtility.FromJson<ControllableData>(file.ReadLine());
            loadData(cData, duration, tweenStyle);
            file.Close();
        }
        catch (Exception e)
        {
            Debug.LogError("Error while loading preset : " + e.StackTrace);
            return;
        }
        currentPreset = fileName;
    }

    //Override it if you want to do things after a load
    public virtual void DataLoaded() { }
    //Override it if you want to do things before a preset save
    public virtual void CallMeBeforeSave() { }

    public virtual void OnDisable()
    {
        if (debug)
            Debug.Log("Saving temp file before destruction");

        if (usePresets)
        {
            if (!string.IsNullOrEmpty(currentPreset))
            {
                //Create temp file
                var tempFile = File.OpenWrite(targetDirectory + tempFileName);
                tempFile.Close();
                //write last loaded preset
                File.WriteAllText(targetDirectory + tempFileName, currentPreset);
            }
        }

        if (debug)
            Debug.Log("Done saving");

        if (debug)
            Debug.Log("Unregistering " + this.GetType().Name + " script on " + this.gameObject.name);
        ControllableMaster.UnRegister(this);
    }

    public FieldInfo getFieldInfoByName(string requestedName)
    {
        var objectFields = GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
        FieldInfo requestedField = null;

        foreach (var item in objectFields)
        {
            if (item.Name == requestedName)
                requestedField = item;
        }

        return requestedField;
    }

    protected void RaiseEventValueChanged(string property)
    {
        if (controllableValueChanged != null) controllableValueChanged(property);
    }

    public void setProp(string property, List<object> values)
    {

        //   if (Properties == null || Methods == null) init();

        MemberInfo info = getPropFieldInfoForAddress(property);

        if (info != null)
        {
            setValueProp(info, values);
            return;
        }

        MethodInfo mInfo = getMethodInfoForAddress(property);
        if (mInfo != null)
        {
            setMethodProp(mInfo, property, values);
            return;
        }
    }

    public void setValueProp(MemberInfo info, List<object> values, bool silent = false)
    {
        string typeString = (info is PropertyInfo) ? (info as PropertyInfo).PropertyType.ToString() : (info as FieldInfo).FieldType.ToString();

        if (debug)
            Debug.Log("Setting attribut  " + info.Name + " of type " + typeString + " with " + values.Count + " value(s) // " + values[0].ToString());

        // if we detect any attribute print out the data.

        if (typeString == "System.Single")
        {
            if (values.Count >= 1) setInfoValue(info, getFloat(values[0]));
        }
        else if (typeString == "System.Boolean")
        {
            if (values.Count >= 1) setInfoValue(info, getBool(values[0]));
        }
        else if (typeString == "System.Int32")
        {
            if (values.Count >= 1) setInfoValue(info, getInt(values[0]));
        }
        else if (typeString == "UnityEngine.Vector2")
        {
            if (values.Count == 1) setInfoValue(info, (Vector2)values[0]);
            if (values.Count >= 2) setInfoValue(info, new Vector2(getFloat(values[0]), getFloat(values[1])));
        }
        else if (typeString == "UnityEngine.Vector3")
        {
            if (values.Count == 1) setInfoValue(info, (Vector3)values[0]);
            if (values.Count >= 3) setInfoValue(info, new Vector3(getFloat(values[0]), getFloat(values[1]), getFloat(values[2])));
        }
        else if (typeString == "UnityEngine.Color")
        {
            if (values.Count == 1) setInfoValue(info, (Color)values[0]);
            else if (values.Count >= 4) setInfoValue(info, new Color(getFloat(values[0]), getFloat(values[1]), getFloat(values[2]), getFloat(values[3])));
            else if (values.Count >= 3) setInfoValue(info, new Color(getFloat(values[0]), getFloat(values[1]), getFloat(values[2]), 1));
        }
        else if (typeString == "System.String")
        {
            // Debug.Log("String received : " + values.ToString());
            setInfoValue(info, values[0].ToString());
        }

        if (uiValueChanged != null) uiValueChanged(info.Name);
        //if (valueChanged != null && !silent) valueChanged(info.Name);
    }

    public void setInfoValue(MemberInfo info, object v)
    {
        if (info is PropertyInfo) (info as PropertyInfo).SetValue(this, v, null);
        else (info as FieldInfo).SetValue(this, v);
    }

    public void setScriptInfoValue(MemberInfo info, object v)
    {
        if (info is PropertyInfo) (info as PropertyInfo).SetValue(TargetScript, v, null);
        else (info as FieldInfo).SetValue(TargetScript, v);
    }

    public object getInfoValue(MemberInfo info)
    {
        if (info is PropertyInfo) return (info as PropertyInfo).GetValue(this, null);
        return (info as FieldInfo).GetValue(this);
    }

    public object getScriptInfoValue(MemberInfo info)
    {
        if (info is PropertyInfo) return (info as PropertyInfo).GetValue(TargetScript, null);
        return (info as FieldInfo).GetValue(TargetScript);
    }

    public Type getInfoType(MemberInfo info)
    {
        return (info is PropertyInfo) ? (info as PropertyInfo).PropertyType : (info as FieldInfo).FieldType;
    }

    public String getInfoTypeString(MemberInfo info)
    {
        return getInfoType(info).ToString();
    }

    public void setMethodProp(MethodInfo info, string property, List<object> values)
    {

        object[] parameters = new object[info.GetParameters().Length];

        OSCMethod attribute = Attribute.GetCustomAttribute(info, typeof(OSCMethod)) as OSCMethod;

        if (debug) Debug.Log("Set Method, num expected parameters : " + (attribute.packInArray ? "1 array" : parameters.Length.ToString()));

        if (attribute.packInArray)
        {
            parameters = new object[1];
            parameters[0] = values.ToArray();
            info.Invoke(this, parameters);
            return;
        }


        int valueIndex = 0;
        for (int i = 0; i < parameters.Length; i++)
        {
            string typeString = info.GetParameters()[i].ParameterType.ToString();
            //Debug.Log("OSC IN Method, arg "+i+" TYPE : " + typeString + ", num values in OSC Message " + values.Count);

            if (typeString == "System.Single")
            {
                if (values.Count >= valueIndex + 1)
                {
                    parameters[i] = getFloat(values[valueIndex]);
                    valueIndex += 1;
                }
            }
            else if (typeString == "System.Boolean")
            {
                if (values.Count >= valueIndex + 1)
                {
                    parameters[i] = getBool(values[valueIndex]);
                    valueIndex += 1;
                }
            }
            else if (typeString == "System.Int32")
            {
                if (values.Count >= valueIndex + 1)
                {
                    parameters[i] = getInt(values[valueIndex]);
                    valueIndex += 1;
                }
            }
            else if (typeString == "UnityEngine.Vector2")
            {
                if (values.Count >= valueIndex + 2)
                {
                    parameters[i] = new Vector2(getFloat(values[valueIndex]), getFloat(values[valueIndex + 1]));
                    valueIndex += 2;
                }
            }
            else if (typeString == "UnityEngine.Vector3")
            {
                if (values.Count >= valueIndex + 3)
                {
                    parameters[i] = new Vector3(getFloat(values[valueIndex]), getFloat(values[valueIndex + 1]), getFloat(values[valueIndex + 2]));
                    valueIndex += 3;
                }
            }
            else if (typeString == "UnityEngine.Color")
            {
                if (values.Count >= valueIndex + 4)
                {
                    parameters[i] = new Color(getFloat(values[valueIndex + 0]), getFloat(values[valueIndex + 1]), getFloat(values[valueIndex + 2]), getFloat(values[valueIndex + 3]));
                    valueIndex += 4;
                }
                else if (values.Count >= i + 3)
                {
                    parameters[i] = new Color(getFloat(values[valueIndex + 0]), getFloat(values[valueIndex + 1]), getFloat(values[valueIndex + 2]), 1);
                    valueIndex += 3;
                }

            }
            else if (typeString == "System.String")
            {
                if (values.Count >= valueIndex + 1)
                {
                    parameters[i] = values[i].ToString();
                    valueIndex += 1;
                }
            }

        }

        info.Invoke(this, parameters);
    }

    public float getFloat(object value)
    {
        Type t = value.GetType();
        if (t == typeof(float)) return (float)value;
        if (t == typeof(int)) return (float)((int)value);
        if (t == typeof(string))
        {
            float result = 0;
            float.TryParse((string)value, out result);
            return result;
        }

        if (t == typeof(bool)) return (bool)value ? 1 : 0;

        return float.NaN;
    }

    public int getInt(object value)
    {
        Type t = value.GetType();
        if (t == typeof(float)) return (int)((float)value);
        if (t == typeof(int)) return (int)value;
        if (t == typeof(string))
        {
            int result = 0;
            int.TryParse((string)value, out result);
            return result;
        }
        if (t == typeof(bool)) return (bool)value ? 1 : 0;

        return 0;
    }

    public bool getBool(object value)
    {
        Type t = value.GetType();
        if (t == typeof(float)) return (float)value >= 1;
        if (t == typeof(int)) return (int)value >= 1;
        if (t == typeof(string))
        {
            string s = ((string)value).ToLower();
            if (s == "true" || s == "1")
                return true;

            if (s == "false" || s == "0")
                return false;

            int result = 0;
            int.TryParse((string)value, out result);

            return result >= 1;
        }
        if (t == typeof(bool)) return (bool)value;

        return false;
    }

    public MemberInfo getPropFieldInfoForAddress(string address)
    {
        foreach (KeyValuePair<string, MemberInfo> p in Properties)
        {
            
            if (p.Key == address)
            {
                return p.Value;
            }
        }

        return null;
    }
    

    public MethodInfo getMethodInfoForAddress(string address)
    {
        foreach (KeyValuePair<string, MethodInfo> p in Methods)
        {
            if (p.Key == address) return p.Value;
        }

        return null;
    }

    public object getData()
    {
        ControllableData data = new ControllableData();
        data.dataID = id;

        foreach (MemberInfo p in Properties.Values)
        {
            OSCProperty attribute = Attribute.GetCustomAttribute(p, typeof(OSCProperty)) as OSCProperty;
            if (attribute.IncludeInPresets)
            {
                string typeString = getInfoTypeString(p);
                if (debug)
                    Debug.Log("Attribute : " + p.Name + " of type " + typeString + " is saved.");

                data.nameList.Add(p.Name);

                //Because a simple "toString" doesn't give the full value
                object val = getInfoValue(p);
                if (typeString == "UnityEngine.Vector3")
                {
                    data.valueList.Add(((Vector3)val).ToString("F8"));
                }
                else if (typeString == "System.Single")
                {
                    data.valueList.Add(((float)val).ToString("F8"));
                }
                else
                    data.valueList.Add(val.ToString());
            }
        }

        return data;
    }

    public void loadData(ControllableData data, float duration = 0, string tweenStyle = null)
    {
        if (tweenStyle != null)
        {
            tweenStyle = tweenStyle.ToLower();
            if (tweenStyle != "easeout" && tweenStyle != "easein" && tweenStyle != "easeinout" && tweenStyle != "linear")
            {
                Debug.LogWarning("Unknow tween style !");
                tweenStyle = null;
            }
        }

        int index = 0;
        foreach (string dn in data.nameList)
        {
            MemberInfo info;
            if (Properties.TryGetValue(dn, out info))
            {
                string type = getInfoTypeString(Properties[dn]);

                if (tweenStyle != null)
                {
                    var curve = new AnimationCurve();

                    if (tweenStyle == "easeinout")
                        curve = TweenCurves.Instance.EaseInOutCurve;

                    else if (tweenStyle == "easein")
                        curve = TweenCurves.Instance.EaseInCurve;

                    else if (tweenStyle == "easeout")
                        curve = TweenCurves.Instance.EaseOutCurve;

                    else if (tweenStyle == "linear")
                        curve = TweenCurves.Instance.LinearCurve;

                    StartCoroutine(
                            TweenValue(Properties[dn],
                                getObjectForValue(type, data.valueList[index]),
                                duration,
                                curve)
                            );
                }
                else
                {
                    List<object> values = new List<object>();
                    values.Add(getObjectForValue(type, data.valueList[index]));
                    setValueProp(Properties[dn], values);
                }
            }

            index++;
        }
        StartCoroutine(WaitForTweenEnd(duration));
    }

    IEnumerator WaitForTweenEnd(float duration)
    {
        var currentTime = 0.0f;
        while (currentTime < duration)
        {
            currentTime += Time.deltaTime;
            yield return new WaitForFixedUpdate();
        }

        DataLoaded();
        if (debug)
            Debug.Log("Done loading.");

        yield return null;
    }

    IEnumerator TweenValue(MemberInfo fieldInfo, object end, float duration, AnimationCurve curve)
    {
        var currentTime = 0f;
        var startValue = getInfoValue(fieldInfo);

        String type = getInfoTypeString(fieldInfo);

        while (currentTime < duration)
        {
            List<object> values = new List<object>();
            //            Debug.Log(fieldInfo.FieldType.ToString() );
            if (type == "System.Single")
            {
                values.Add(Mathf.Lerp((float)startValue, (float)end, curve.Evaluate(currentTime / duration)));
            }

            else if (type == "System.Int32")
            {
                values.Add((int)Mathf.Lerp((int)startValue, (int)end, curve.Evaluate(currentTime / duration)));
            }

            else if (type == "UnityEngine.Vector2")
            {
                values.Add(Vector2.Lerp((Vector2)startValue, (Vector2)end, curve.Evaluate(currentTime / duration)));
            }

            else if (type == "UnityEngine.Vector3")
            {
                values.Add(Vector3.Lerp((Vector3)startValue, (Vector3)end, curve.Evaluate(currentTime / duration)));
            }

            else if (type == "UnityEngine.Color")
            {
                values.Add(Color.Lerp((Color)startValue, (Color)end, curve.Evaluate(currentTime / duration)));
            }
            else
            {
                break;
            }
            setValueProp(fieldInfo, values);
            currentTime += Time.deltaTime;

            yield return new WaitForFixedUpdate();
        }

        List<object> finalValue = new List<object>();
        finalValue.Add(end);
        setValueProp(fieldInfo, finalValue);

        yield return 0;
    }

    object getObjectForValue(string typeString, string value)
    {
        if (typeString == "System.Single") return getFloat(value);
        if (typeString == "System.Boolean") return getBool(value);
        if (typeString == "System.Int32") return getInt(value);
        if (typeString == "UnityEngine.Vector2") return StringToVector2(value);
        if (typeString == "UnityEngine.Vector3") return StringToVector3(value);
        if (typeString == "UnityEngine.Color") return StringToColor(value);
        if (typeString == "System.String") return value;

        return null;
    }


    public static Color StringToColor(string sColor)
    {
        // Remove the parentheses
        if (sColor.StartsWith("RGBA(") && sColor.EndsWith(")"))
        {
            sColor = sColor.Substring(5, sColor.Length - 6);
        }

        // split the items
        string[] sArray = sColor.Split(',');

        // store as a Vector3
        Color result = new Color(
            float.Parse(sArray[0]),
            float.Parse(sArray[1]),
            float.Parse(sArray[2]),
            float.Parse(sArray[3])
        );


        return result;
    }

    public static Vector2 StringToVector2(string sVector)
    {
        // Remove the parentheses
        if (sVector.StartsWith("(") && sVector.EndsWith(")"))
        {
            sVector = sVector.Substring(1, sVector.Length - 2);
        }

        // split the items
        string[] sArray = sVector.Split(',');

        // store as a Vector3
        Vector2 result = new Vector2(
            float.Parse(sArray[0]),
            float.Parse(sArray[1])
            );

        return result;
    }

    public static Vector3 StringToVector3(string sVector)
    {
        // Remove the parentheses
        if (sVector.StartsWith("(") && sVector.EndsWith(")"))
        {
            sVector = sVector.Substring(1, sVector.Length - 2);
        }

        // split the items
        string[] sArray = sVector.Split(',');

        // store as a Vector3
        Vector3 result = new Vector3(
            float.Parse(sArray[0]),
            float.Parse(sArray[1]),
            float.Parse(sArray[2]));

        return result;
    }

}
