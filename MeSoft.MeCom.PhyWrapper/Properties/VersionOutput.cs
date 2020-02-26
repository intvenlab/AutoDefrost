#define IS_RELEASED 

using System.Reflection;
#if (IS_RELEASED)
[assembly: AssemblyVersion(VersionDefinition.Major + "." + VersionDefinition.Minor + ".2.113")]
[assembly: AssemblyCopyright("Copyright ©  2019 - Released")]
#else
[assembly: AssemblyVersion(VersionDefinition.Major + "." + VersionDefinition.Minor + ".1.113")]
[assembly: AssemblyCopyright("Copyright ©  2019 - Committed")]
#endif


/// <summary>Defines the version of the assembly.</summary>
static internal class VersionDefinition
{
    /// <summary>Big version before the dot.</summary>
    public const string Major = "1";
    /// <summary>Small version behind the dot.</summary>
    public const string Minor = "45";
}

/*
Set the VersionInput.cs file as "Build Action" "None".
Include the resulting VersionOutput.cs in project.
Use the following 2 lines as "Pre-build event command line":
cd "$(ProjectDir)"
SubWCRev.exe ../  Properties\VersionInput.cs Properties\VersionOutput.cs
*/


