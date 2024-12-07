using Pulumi;
using Pulumi.Digitalocean;
using System.Collections.Generic;

return await Deployment.RunAsync(() =>
{
    // Define configuration options
    var config = new Config();
    var dropletSize = config.Get("dropletSize") ?? "s-1vcpu-1gb";
    var region = config.Get("region") ?? "nyc1"; // default to New York data center

    // Retrieve the Tailscale auth key from Pulumi secrets
    var tailscaleAuthKey = config.RequireSecret("tailscaleAuthKey");

    // Define cloud-init script to join Tailscale with the provided auth key
    var cloudInitScript = @"
        #cloud-config
        runcmd:
          - curl -fsSL https://tailscale.com/install.sh | sh
          - tailscale up --authkey " + tailscaleAuthKey.Apply(key => key) + @"
    ";

    // Create a new Droplet instance
    var droplet = new Droplet("my-droplet", new DropletArgs
    {
        Size = dropletSize,
        Region = region,
        Image = "ubuntu-20-04-x64",
        UserData = cloudInitScript // Add the cloud-init script here
    });

    // Export Droplet's Public IP
    return new Dictionary<string, object?>
    {
        ["dropletPublicIp"] = droplet.Ipv4Address
    };
});