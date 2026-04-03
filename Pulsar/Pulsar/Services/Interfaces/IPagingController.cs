using System;

namespace Pulsar.Services.Interfaces
{
    public interface IPagingController
    {
        int CurrentPage { get; }
        int TotalPages { get; }
        event EventHandler<BoundaryReachedEventArgs>? OnBoundaryReached;
        Task NextPageAsync();
        Task PrevPageAsync();
        Task GoToPageAsync(int pageIndex);
        void SetTotalPages(int totalPages);
    }

    public class BoundaryReachedEventArgs : EventArgs
    {
        public BoundaryReachedEventArgs(BoundaryDirection direction)
        {
            Direction = direction;
        }

        public BoundaryDirection Direction { get; }
    }

    public enum BoundaryDirection
    {
        FirstPage,
        LastPage
    }
}
