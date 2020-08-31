namespace NServiceBus.Transport.InMemory
{
    /// <summary>
    /// Meta data about the end point.
    /// </summary>
    public class EndpointInfo
    {
        public EndpointInfo(string name)
        {
            Name = name;
        }

        /// <summary>
        /// The end point name.
        /// </summary>
        public string Name { get; }
    }
}
