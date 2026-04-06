using Vintagestory.API.Common;

namespace effectshud.src
{
    public class TrackedEffect
    {
        public AssetLocation [] effectTexture { get; set; }
        public bool showTime { get; set; }

        public string watchedBranch { get; set; }
        public string effectWatchedName { get; set; }
        public string effectDurationWatchedName { get; set; }
        public Vintagestory.API.Common.Func<int, bool> getTextureNumber { get; set; } = null;
        public bool active { get; set; } = false;
        public int activeTexture { get; set; } = 0;

        public TrackedEffect(AssetLocation [] assetLocation, bool showTime, string watchedBranch, string effectWatchedName, string effectDurationWatchedName, Vintagestory.API.Common.Func<int, bool> checkAction)
        {
            this.getTextureNumber = checkAction;
            this.watchedBranch = watchedBranch;
            effectTexture = assetLocation;
            this.showTime = showTime;
            if(!showTime)
            {
                this.effectDurationWatchedName = "";
            }
            else
            {
                this.effectDurationWatchedName = effectDurationWatchedName;
            }
            this.effectWatchedName = effectWatchedName;
            
        }


    }
}
