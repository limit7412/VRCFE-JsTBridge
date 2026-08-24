using System.Runtime.CompilerServices;

// Editorアセンブリからinternalメンバー（EditorOnValidateHook）へアクセスするため
[assembly: InternalsVisibleTo("FEJsTBridge.Editor")]

// テストアセンブリからinternalメンバーへアクセスするため
[assembly: InternalsVisibleTo("FEJsTBridge.Editor.Tests")]
