using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.ViewModels.Settings;
using Pulsar.Views.Controls;
using Xunit;

namespace Pulsar.Tests.Views.Controls
{
    public class SlotContextMenuBuilderTests
    {
        private const int SlotsPerPage = 8;

        private static ILocalizationService CreateLoc()
        {
            return new LocalizationService(new Mock<ILogger<LocalizationService>>().Object);
        }

        private static SlotWheelEditorViewModel CreateVm()
        {
            return new SlotWheelEditorViewModel(
                new SlotLayoutEngine(),
                CreateLoc(),
                new CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger());
        }

        private static PluginSlot CreateSlot()
        {
            return new PluginSlot
            {
                Label = "Test",
                Slot = 1,
                PluginId = "com.pulsar.command",
                IconKey = "E756"
            };
        }

        [Fact]
        public void Build_ContainsMoveToEditAndDelete()
        {
            RunInSta(() =>
            {
                var vm = CreateVm();
                vm.SetSlots(new System.Collections.ObjectModel.ObservableCollection<PluginSlot> { CreateSlot() }, SlotsPerPage);
                var builder = new SlotContextMenuBuilder(CreateLoc());

                var menu = builder.Build(CreateSlot(), vm);

                menu.Items.Count.Should().Be(4); // Move to, separator, Edit, Delete
                menu.Items[0].Should().BeOfType<MenuItem>().Which.Header.Should().NotBeNull();
                menu.Items[2].Should().BeOfType<MenuItem>();
                menu.Items[3].Should().BeOfType<MenuItem>();
            });
        }

        [Fact]
        public void Build_MoveToContainsPageAndSlotItems()
        {
            RunInSta(() =>
            {
                var vm = CreateVm();
                vm.SetSlots(new System.Collections.ObjectModel.ObservableCollection<PluginSlot> { CreateSlot() }, SlotsPerPage);
                var builder = new SlotContextMenuBuilder(CreateLoc());

                var menu = builder.Build(CreateSlot(), vm);
                var moveTo = menu.Items[0] as MenuItem;

                moveTo.Should().NotBeNull();
                moveTo!.Items.Count.Should().Be(vm.TotalPages);
                var firstPage = moveTo.Items[0] as MenuItem;
                firstPage.Should().NotBeNull();
                firstPage!.Items.Count.Should().Be(vm.SlotsPerPage);
            });
        }

        [Fact]
        public void EditClick_InvokesOnEdit()
        {
            RunInSta(() =>
            {
                var vm = CreateVm();
                vm.SetSlots(new System.Collections.ObjectModel.ObservableCollection<PluginSlot> { CreateSlot() }, SlotsPerPage);
                var builder = new SlotContextMenuBuilder(CreateLoc());
                var slot = CreateSlot();
                var menu = builder.Build(slot, vm);
                PluginSlot? received = null;
                builder.OnEdit = s => received = s;

                (menu.Items[2] as MenuItem)!.RaiseEvent(new System.Windows.RoutedEventArgs(MenuItem.ClickEvent));

                received.Should().BeSameAs(slot);
            });
        }

        [Fact]
        public void DeleteClick_InvokesOnDelete()
        {
            RunInSta(() =>
            {
                var vm = CreateVm();
                vm.SetSlots(new System.Collections.ObjectModel.ObservableCollection<PluginSlot> { CreateSlot() }, SlotsPerPage);
                var builder = new SlotContextMenuBuilder(CreateLoc());
                var slot = CreateSlot();
                var menu = builder.Build(slot, vm);
                PluginSlot? received = null;
                builder.OnDelete = s => received = s;

                (menu.Items[3] as MenuItem)!.RaiseEvent(new System.Windows.RoutedEventArgs(MenuItem.ClickEvent));

                received.Should().BeSameAs(slot);
            });
        }

        private static void RunInSta(Action action)
        {
            Exception? capturedException = null;
            using var completed = new ManualResetEventSlim(false);

            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    capturedException = ex;
                }
                finally
                {
                    completed.Set();
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            completed.Wait();
            thread.Join();

            if (capturedException != null)
            {
                ExceptionDispatchInfo.Capture(capturedException).Throw();
            }
        }
    }
}
