using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Reiga.VisualNodeEditor.Tests.Editor")]

// ADV Starter とゲームの Starter が Play と同じやり方で Graph Runner を置き（PlaySetup）、シーンを作り（ScenePicker）、
// スクリプトのクラス名が重ならないか確かめるため（NodeBehaviourScriptCreator）
[assembly: InternalsVisibleTo("Reiga.VisualNodeEditor.YarnSpinner.Editor")]
[assembly: InternalsVisibleTo("Reiga.VisualNodeEditor.Starters.Editor")]
