using System;

public class OSCMetadata : Attribute
{
    public string customName;
}

[AttributeUsage(AttributeTargets.Field|AttributeTargets.Property)]
public class OSCProperty : OSCMetadata
{
    public string TargetList;
    public bool IncludeInPresets = true;
    public bool ShowInUI = true;
    public bool isInteractible = true;

}

[AttributeUsage(AttributeTargets.Method)]
public class OSCMethod : OSCMetadata
{
	public bool packInArray;
}