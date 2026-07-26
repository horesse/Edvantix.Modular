using EDV.Framework.Web.Modules;
using EDV.Modules.Identity;
using System.Runtime.CompilerServices;

[assembly: Module(typeof(IdentityModule), 100)]
[assembly: InternalsVisibleTo("Identity.Tests")]