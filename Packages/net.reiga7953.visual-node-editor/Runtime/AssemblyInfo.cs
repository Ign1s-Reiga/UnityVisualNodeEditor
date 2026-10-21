using System.Runtime.CompilerServices;

// 貼り付け時の ID の振り直しなど、エディタだけが行う操作を公開 API にしないため
[assembly: InternalsVisibleTo("Reiga.VisualNodeEditor.Editor")]
[assembly: InternalsVisibleTo("Reiga.VisualNodeEditor.Tests.Editor")]

// Yarn Spinner 連携のテストから、セーブの置き場（GraphSaveSlots.RootOverride）を差し替えるため
[assembly: InternalsVisibleTo("Reiga.VisualNodeEditor.YarnSpinner.Tests")]

// Localization 連携の変数が、Runner の追い方（GraphRunnerFollower）を UI Toolkit のコントロールと共有するため
[assembly: InternalsVisibleTo("Reiga.VisualNodeEditor.Localization")]
