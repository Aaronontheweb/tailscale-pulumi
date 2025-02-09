using Pulumi;
using Pulumi.DigitalOcean;
using Pulumi.Tls;
using System.Collections.Generic;

return await Deployment.RunAsync(() =>
{
    // Define configuration options
    var config = new Pulumi.Config();
    var dropletSize = config.Require("dropletSize");
    var region = config.Require("dropletRegion");
    var image = config.Require("dropletImage");

    // Retrieve the Tailscale auth key from Pulumi secrets
    var tailscaleAuthKey = config.RequireSecret("tailscaleAuthKey");

    // Generate a new SSH key pair
    var sshKeyPair = new PrivateKey("ssh-key", new PrivateKeyArgs
    {
        Algorithm = "RSA",
        RsaBits = 4096,
    });

    // Create a DigitalOcean SSH key from the generated public key
    var sshKey = new SshKey("droplet-ssh-key", new SshKeyArgs
    {
        Name = "generated-ssh-key",
        PublicKey = sshKeyPair.PublicKeyOpenssh,
    });

    // Define a script to be run when the VM starts up
    var initScript = tailscaleAuthKey.Apply(authKey => $$"""
                                                         #!/bin/bash
                                                                 set -eux  # Exit on error and print commands as they are executed
                                                         
                                                                 # Log the start of Tailscale installation
                                                                 echo 'Starting Tailscale installation...' > /tmp/tailscale-setup.log
                                                         
                                                                 # Wait for apt-get to release the lock
                                                                 while sudo fuser /var/lib/apt/lists/lock >/dev/null 2>&1; do
                                                                     echo 'Waiting for apt lock...' >> /tmp/tailscale-setup.log
                                                                     sleep 5
                                                                 done
                                                         
                                                                 # Retry mechanism for downloading and installing Tailscale
                                                                 for i in {1..5}; do
                                                                     curl -fsSL https://tailscale.com/install.sh | sh && break || {
                                                                         echo 'Tailscale installation failed, retrying in 5 seconds...' >> /tmp/tailscale-setup.log
                                                                         sleep 5
                                                                     }
                                                                 done
                                                         
                                                                 # Check if Tailscale was installed
                                                                 if ! command -v tailscale > /dev/null; then
                                                                     echo 'Tailscale installation failed after 5 attempts' >> /tmp/tailscale-setup.log
                                                                     exit 1
                                                                 fi
                                                                 
                                                                 # Enable IP forwarding
                                                                 echo 'net.ipv4.ip_forward = 1' | sudo tee -a /etc/sysctl.d/99-tailscale.conf
                                                                 echo 'net.ipv6.conf.all.forwarding = 1' | sudo tee -a /etc/sysctl.d/99-tailscale.conf
                                                                 sudo sysctl -p /etc/sysctl.d/99-tailscale.conf
                                                         
                                                                 # Start Tailscale with the provided auth key and enable SSH
                                                                 echo 'Starting Tailscale...' >> /tmp/tailscale-setup.log
                                                                 sudo tailscale up --authkey="{{authKey}}" --ssh --advertise-exit-node --accept-routes >> /tmp/tailscale-setup.log 2>&1
                                                         
                                                                 # Verify Tailscale status
                                                                 tailscale status >> /tmp/tailscale-setup.log 2>&1

                                                         """);

    // Create a DigitalOcean VPC
    var vpc = new Vpc($"my-vpc-{region}", new VpcArgs
    {
        Region = region,
        Name = $"tailscale-vpc-{region}",
        IpRange = "10.156.11.0/24", // Ensure no overlap with other networks
    });

    var randomNodeName = new Pulumi.Random.RandomString("server-name", new Pulumi.Random.RandomStringArgs
    {
        Length = 4,
        Special = false,
        Upper = false,
        Number = false,
    });

    // make sure the name is unique and somewhat randomized so we don't have to purge SSH fingerprints
    // and make sure the region name is included in case we have multiple exit-nodes
    var dropletName = randomNodeName.Result.Apply(c => $"tailscale-droplet-{region}-{c}");

    // Create a new Droplet instance
    var droplet = new Droplet("tailscale-droplet", new DropletArgs
    {
        Name = dropletName,
        Size = dropletSize,
        Region = region,
        Image = image,
        VpcUuid = vpc.Id,
        UserData = initScript,
        Ipv6 = true,
        SshKeys = new InputList<string> { sshKey.Fingerprint }, // Attach the SSH key
        Monitoring = true, // enable free Digital Ocean monitoring
    });

    // Export Droplet's IP and generated credentials
    return new Dictionary<string, object?>
    {
        ["dropletIp"] = droplet.Ipv4Address,
        ["dropLetName"] = dropletName,
        ["privateKeyPem"] = sshKeyPair.PrivateKeyPem // Export the private key securely
    };
});