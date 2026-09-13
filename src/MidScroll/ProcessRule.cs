namespace MidScroll
{
    // 監視対象プロセス1件分。設定画面のリストで個別にON/OFFできるように
    // 名前とは別に有効フラグを持つ。
    internal sealed class ProcessRule
    {
        public string Name { get; set; } = "";
        public bool Enabled { get; set; } = true;
    }
}
