using System.Reflection;
using System.Runtime.Loader;

namespace Kevahu.Microservices.Orchestrator.Loading
{
    /// <summary>
    /// Represents a custom <see cref="AssemblyLoadContext"/> for loading microservice assemblies
    /// from a specific directory. It allows for loading assemblies and their dependencies in an
    /// isolated context, prioritizing loading from the default context before attempting to load
    /// from the specified services directory.
    /// </summary>
    public class ServicesLoadContext : AssemblyLoadContext
    {
        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ServicesLoadContext"/> class, making it collectible.
        /// </summary>
        /// <param name="services">The directory containing the microservice assemblies to load.</param>
        public ServicesLoadContext(DirectoryInfo services) : base("Microservices", true)
        {
            ServicesDirectory = services;
        }

        #endregion Public Constructors

        #region Properties

        /// <summary>
        /// Gets or sets the directory from which microservice assemblies are loaded by this context.
        /// </summary>
        public DirectoryInfo ServicesDirectory { get; set; }

        #endregion Properties

        #region Public Methods

        /// <summary>
        /// Scans the <see cref="ServicesDirectory"/> for DLL files, loads them into this context if
        /// not already present, and returns all assemblies currently loaded within this specific context.
        /// </summary>
        /// <returns>
        /// An <see cref="IEnumerable{Assembly}"/> containing all assemblies loaded in this context.
        /// </returns>
        public IEnumerable<Assembly> FindServices()
        {
            HashSet<string> location = Assemblies.Select(a => a.Location).ToHashSet();
            foreach (FileInfo file in ServicesDirectory.EnumerateFiles("*.dll"))
            {
                if (!location.Contains(file.FullName))
                {
                    LoadFromAssemblyPath(file.FullName);
                    location.Add(file.FullName);
                }
            }
            return Assemblies;
        }

        #endregion Public Methods

        #region Protected Methods

        /// <summary>
        /// Overrides the default assembly loading behavior for this context. It first attempts to
        /// load the requested assembly from the <see cref="AssemblyLoadContext.Default"/> context.
        /// If the assembly is not found there, it then attempts to load it directly from a DLL file
        /// (matching the assembly name) located within the <see cref="ServicesDirectory"/>.
        /// </summary>
        /// <param name="assemblyName">The <see cref="AssemblyName"/> of the assembly to load.</param>
        /// <returns>
        /// The loaded <see cref="Assembly"/>, or null if the assembly could not be found in either
        /// the default context or the services directory.
        /// </returns>
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            return Default.LoadFromAssemblyName(assemblyName) ?? LoadFromAssemblyPath(Path.Combine(ServicesDirectory.FullName, $"{assemblyName.Name}.dll"));
        }

        #endregion Protected Methods
    }
}