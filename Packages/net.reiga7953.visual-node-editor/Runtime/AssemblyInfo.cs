using System.Runtime.CompilerServices;

// 貼り付け時の ID の振り直しなど、エディタだけが行う操作を公開 API にしないため
[assembly: InternalsVisibleTo("Reiga.VisualNodeEditor.Editor")]
[assembly: InternalsVisibleTo("Reiga.VisualNodeEditor.Tests.Editor")]
