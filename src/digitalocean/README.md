# Tailscale SSH + Exit Node on Digital Ocean Droplet

If you're traveling abroad and want to secure your hotel / conference / coffee shop WiFi, setting up a [Tailscale](https://tailscale.com/) exit node on a Digital Ocean droplet is a fairly inexpensive way to do it.

This sample uses Pulumi to automatically provision a new exit node to your tailnet using a configurably-sized Digital Ocean droplet.

## Prerequisites

To use this you will need:

* Valid Pulumi account (free tier is fine);
* Tailscale tailnet and ACLs setup to allow `tailscale ssh` to work properly (default ACLs should be ok); and
* A Digitial Ocean account + API key that will allow you to provision new droplets and VPCs.
* The .NET SDK installed. See [`global.json`](../../global.json) for version information.

## Running This App

### 1 - Clone and Compile

`git clone` this repository and run this directory locally on your machine first and then run `dotnet build` to ensure that the project itself compiles and restores.

### 2 - Login to Pulumi on Local Machine

You will need to [install the `pulumi` tools](https://www.pulumi.com/docs/iac/download-install/) on your local machine and then use `pulumi login` to access your pulumi account.

### 3 - Create and Set Digital Ocean API Key

In order to authenticate Pulumi to modify or create Digital Ocean deployments, we need to set the `DIGITALOCEAN_TOKEN` environment variable with a Digital Ocean API token that has the appropriate scopes.

You can create and manage a token here: [https://cloud.digitalocean.com/account/api/tokens](https://cloud.digitalocean.com/account/api/tokens)

> [!note]
> For ease of use I recommend using a "full scope" token with a very short expiration date, assuming you're using a personal Digital Ocean account. If you're part of a larger organization with many resources already deployed on DO, you might want a custom scope.

Once you have your token, you can set the environment variable ephemerally (so it will no longer exist after your current shell session ends).

On Linux / OS X:

```bash
export DIGITALOCEAN_TOKEN="your_token_here"
```

On Windows with Powershell:

```
$env:DIGITALOCEAN_TOKEN = "your_token_here"
```

### 4 - Create a Tailscale Auth Key

This section will depend on how your [Tailscle ACLs](https://tailscale.com/kb/1192/acl-samples) are defined - for now, we are assuming you have figured out how to provision tailnet devices where [`tailnet ssh`](https://tailscale.com/kb/1193/tailscale-ssh) can function correctly (usually, the default ACLs, which are quite permissive, will work fine.)

Create a [Tailscale authentication key](https://login.tailscale.com/admin/settings/keys) that will provision the node with the correct permissions (`tag`s from the ACLs are the correct tool for the job here) and then set that as a secret in our Pulumi configuration:

```shell
pulumi config set tailscaleAuthKey <your-tailscale-auth-key> --secret
```

### 5 - Optionally, Configure Droplet Size / Data Center / VM Image

This application comes pre-configured with all of the following settings:

```yaml
do-tailscale-exitnode:dropletImage: ubuntu-24-04-x64
do-tailscale-exitnode:dropletRegion: nyc1
do-tailscale-exitnode:dropletSize: s-1vcpu-1gb
```

This means we're deploying a Ubuntu 24.04 VM in Digital Ocean's NYC1 data center using the smallest available [Droplet plan](https://www.digitalocean.com/pricing/droplets).

You can change and of these values via the `pulumi config set` CLI:

```shell
pulumi config set dropletRegion lon1
```

Now this droplet will be deployed to London instead of NYC.

### 6 - Deploy!

To deploy, just run `pulumi up` on your project.