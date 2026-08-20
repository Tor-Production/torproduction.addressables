namespace TorProduction.AddressablesToolpack.Data
{
    public interface ITemplate
    {
        string GetId { get; }
        void SetId(string id);

        string Name { get; }
        string Description { get; set; }
    }
}
