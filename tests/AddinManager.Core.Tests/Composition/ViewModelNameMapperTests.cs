using System.Reflection;
using AddinManager.Core.Composition;

namespace Mapping.Views
{
    public static class CardView
    {
        public static string Marker => nameof(CardView);
    }

    public static class OrphanView
    {
        public static string Marker => nameof(OrphanView);
    }
}

namespace Mapping.ViewModels
{
    public static class CardViewModel
    {
        public static string Marker => nameof(CardViewModel);
    }
}

namespace SamePlace
{
    public static class BoxView
    {
        public static string Marker => nameof(BoxView);
    }

    public static class BoxViewModel
    {
        public static string Marker => nameof(BoxViewModel);
    }
}

namespace AddinManager.Core.Tests.Composition
{
    public sealed class ViewModelNameMapperTests
    {
        private static readonly Assembly Cases = typeof(Mapping.Views.CardView).Assembly;

        [Fact]
        public void ViewsNamespace_MapsToViewModelsNamespace()
        {
            Assert.Same(typeof(Mapping.ViewModels.CardViewModel), ViewModelNameMapper.ResolveViewModelType(typeof(Mapping.Views.CardView), Cases));
        }

        [Fact]
        public void SameNamespace_MapsByName()
        {
            Assert.Same(typeof(SamePlace.BoxViewModel), ViewModelNameMapper.ResolveViewModelType(typeof(SamePlace.BoxView), Cases));
        }

        [Fact]
        public void MissingViewModel_ReturnsNull()
        {
            Assert.Null(ViewModelNameMapper.ResolveViewModelType(typeof(Mapping.Views.OrphanView), Cases));
        }

        [Fact]
        public void NonViewName_ReturnsNull()
        {
            Assert.Null(ViewModelNameMapper.ResolveViewModelType(typeof(string), Cases));
        }
    }
}

