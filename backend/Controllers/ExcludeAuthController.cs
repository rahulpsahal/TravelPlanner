using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace TravelPlanner.Api.Controllers;

internal sealed class ExcludeAuthController : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature) =>
        feature.Controllers.Remove(typeof(AuthController).GetTypeInfo());
}
