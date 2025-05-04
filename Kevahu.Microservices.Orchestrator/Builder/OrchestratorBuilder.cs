using Kevahu.Microservices.Orchestrator.Loading;

namespace Kevahu.Microservices.Orchestrator.Builder
{
    /// <summary>
    /// Provides a fluent interface for configuring and building an <see
    /// cref="OrchestratorInitiator"/>. Manages configuration options and raises events during the
    /// build process.
    /// </summary>
    public class OrchestratorBuilder
    {
        #region Classes

        /// <summary>
        /// Provides data for the <see cref="OrchestratorBuilder.OnServicesLoaded"/> event.
        /// </summary>
        public class ServicesLoadedEventArgs : EventArgs
        {
            #region Properties

            /// <summary>
            /// Gets the context containing information about the loaded services.
            /// </summary>
            public ServicesLoadContext ServicesLoadContext { get; set; }

            #endregion Properties
        }

        #endregion Classes

        #region Properties

        /// <summary>
        /// Gets the configuration options for the orchestrator.
        /// </summary>
        public OrchestratorOptions Options { get; set; } = new OrchestratorOptions();

        #endregion Properties

        #region Events

        /// <summary>
        /// Occurs after services have been loaded, typically during the build process. Provides
        /// access to the <see cref="Loading.ServicesLoadContext"/>.
        /// </summary>
        public event EventHandler<ServicesLoadedEventArgs> OnServicesLoaded;

        #endregion Events

        #region Public Methods

        /// <summary>
        /// Creates and returns a new <see cref="OrchestratorInitiator"/> instance based on the
        /// configured options.
        /// </summary>
        /// <returns>A new <see cref="OrchestratorInitiator"/> instance.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <see cref="Options"/> is null when Build is called.
        /// </exception>
        public OrchestratorInitiator Build()
        {
            if (Options == null)
            {
                throw new InvalidOperationException("Options must be set before building the OrchestratorInitiator.");
            }
            return new OrchestratorInitiator(this);
        }

        /// <summary>
        /// Raises the <see cref="OnServicesLoaded"/> event.
        /// </summary>
        /// <param name="servicesLoadContext">
        /// The context containing information about the loaded services.
        /// </param>
        public void InvokeServicesLoaded(ServicesLoadContext servicesLoadContext)
        {
            OnServicesLoaded?.Invoke(this, new ServicesLoadedEventArgs
            {
                ServicesLoadContext = servicesLoadContext
            });
        }

        #endregion Public Methods
    }
}