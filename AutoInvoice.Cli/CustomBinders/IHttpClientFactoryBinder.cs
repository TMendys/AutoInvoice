using System.CommandLine.Binding;

namespace AutoInvoice.CLI.CustomBinders;

public class IHttpClientFactoryBinder : BinderBase<IHttpClientFactory>
{
    protected override IHttpClientFactory GetBoundValue(BindingContext bindingContext)
    {
        // bindingContext.AddService<IHttpClientFactory>(factory=>);
        throw new NotImplementedException();
    }

    // IHttpClientFactory GetHttpClientFactory(BindingContext bindingContext)
    // {

    //     return IHttpClientFactory;
    // }
}