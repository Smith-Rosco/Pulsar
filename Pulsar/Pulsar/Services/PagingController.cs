using Pulsar.Services.Interfaces;
using System;
using System.Threading.Tasks;

namespace Pulsar.Services
{
    public class PagingController : IPagingController
    {
        private readonly IAnimationController _animationController;
        private int _currentPage;
        private int _totalPages = 1;

        public PagingController(IAnimationController animationController)
        {
            _animationController = animationController;
        }

        public int CurrentPage => _currentPage;
        public int TotalPages => _totalPages;

        public event EventHandler<BoundaryReachedEventArgs>? OnBoundaryReached;

        public void SetTotalPages(int totalPages)
        {
            _totalPages = totalPages;
            if (_currentPage >= _totalPages)
            {
                _currentPage = Math.Max(0, _totalPages - 1);
            }
        }

        public async Task NextPageAsync()
        {
            if (_currentPage >= _totalPages - 1)
            {
                OnBoundaryReached?.Invoke(this, new BoundaryReachedEventArgs(BoundaryDirection.LastPage));
                await _animationController.BounceAsync(BounceDirection.LastPage);
                return;
            }

            _currentPage++;
        }

        public async Task PrevPageAsync()
        {
            if (_currentPage <= 0)
            {
                OnBoundaryReached?.Invoke(this, new BoundaryReachedEventArgs(BoundaryDirection.FirstPage));
                await _animationController.BounceAsync(BounceDirection.FirstPage);
                return;
            }

            _currentPage--;
        }

        public Task GoToPageAsync(int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= _totalPages)
            {
                return Task.CompletedTask;
            }

            _currentPage = pageIndex;
            return Task.CompletedTask;
        }
    }
}
