using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace IwaraDownloader.Wpf.Models
{
    /// <summary>
    /// ObservableCollection.Add()を件数分呼ぶと1件ごとにCollectionChangedが発火し、
    /// バインド先のListView等がそのたびに差分反映しようとするため、数万件規模の
    /// 一括差し替えではUIスレッドが長時間ブロックされる。ReplaceAllは内部リストを
    /// 直接差し替えてから単発のResetイベントだけを発火する。
    /// </summary>
    public class BulkObservableCollection<T> : ObservableCollection<T>
    {
        public void ReplaceAll(IEnumerable<T> items)
        {
            Items.Clear();
            foreach (var item in items)
                Items.Add(item);

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
