using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace SER.Graphql.Reflection.NetCore.WebSocket
{
    public interface IHandleMsg<T>
    {
        IObservable<T> ObservableObj();
        ISubject<T> GetStream();
        void AddError(Exception exception);
    }

    public class HandleMsg<T> : IHandleMsg<T>
    {
        private readonly ISubject<T> _messageStream = new ReplaySubject<T>(1);

        public IObservable<T> ObservableObj()
        {
            return _messageStream.AsObservable();
        }

        public void AddError(Exception exception) => _messageStream.OnError(exception);

        public ISubject<T> GetStream()
        {
            return _messageStream;
        }
    }
}
