using System.Runtime.CompilerServices;

// The rules tests make up their own classes and options, and an option changes a skill through the same setters the
// class catalog uses. Nothing else outside the rules gets to change a skill: the game only reads them.
[assembly: InternalsVisibleTo("FiveKingdoms.Tests.EditMode")]
