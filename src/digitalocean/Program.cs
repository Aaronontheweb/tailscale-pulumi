using Pulumi;
using System.Collections.Generic;
using Pulumi.DigitalOcean;
using Config = Pulumi.Config;

return await Deployment.RunAsync(() =>
{
    // Define configuration options
    var config = new Config();
    var dropletSize = config.Require("dropletSize");
    var region = config.Require("dropletRegion");
    var image = config.Require("dropletImage");

    // Retrieve the Tailscale auth key from Pulumi secrets
    var tailscaleAuthKey = config.RequireSecret("tailscaleAuthKey");

    // Define cloud-init script to join Tailscale with the provided auth key
    var cloudInitScript = $"""
                             
                             #cloud-config
                             runcmd:
                               - curl -fsSL https://tailscale.com/install.sh | sh
                               - tailscale up --authkey {tailscaleAuthKey.Apply(key => key)} --accept-routes --ssh --advertise-exit-node
                           """;
    
    // Create a DigitalOcean VPC
    var vpc = new Vpc("my-vpc", new VpcArgs
    {
        Region = region,
        IpRange = "10.10.10.0/24", // make sure this doesn't overlap with other networks on your account
    });

    // Create a new Droplet instance
    var droplet = new Droplet("tailscale-droplet", new DropletArgs
    {
        Name = "tailscale-droplet",
        Size = dropletSize,
        Region = region,
        Image = image,
        VpcUuid = vpc.Id,
        UserData = cloudInitScript // Add the cloud-init script here
    });

    // Export Droplet's IP
    return new Dictionary<string, object?>
    {
        ["dropletIp"] = droplet.Ipv4Address
    };
});