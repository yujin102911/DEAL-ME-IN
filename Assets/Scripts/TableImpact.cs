using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NumberTable
{
    public partial class TableView
    {
        private AudioClip impactSound, coinTick;
        private AudioClip jackpotSound, crashSound;
        private RectTransform impactTable;
        private Vector2 impactOrigin;
        private Text impactTotal;
        private void RestoreImpact()
        {
            if(impactTable!=null)impactTable.anchoredPosition=impactOrigin;
            if(impactTotal!=null)impactTotal.enabled=true;
            impactTable=null;impactTotal=null;
        }
        private void FocusImpact()
        {
            impactTable=page.Find("Playing Table") as RectTransform;
            if(impactTable!=null)impactOrigin=impactTable.anchoredPosition;
            impactTotal=LabelAt("Playing Table/Total");
            if(impactTotal!=null)impactTotal.enabled=false;
        }
        private void ShakeImpact(float t,float strength)
        {
            if(impactTable!=null)impactTable.anchoredPosition=impactOrigin+
                new Vector2(Mathf.Sin(t*83),Mathf.Cos(t*67))*strength*(1-t)*(1-t);
        }
        private Text ImpactTitle(Transform parent,string name,float y,string value,int size,Color color)
        {
            var text=Text(parent,name,356,y,720,150,value,size,color,TextAnchor.MiddleCenter);
            text.resizeTextForBestFit=true;text.resizeTextMinSize=28;text.resizeTextMaxSize=size;
            text.rectTransform.pivot=new Vector2(.5f,.5f);
            text.rectTransform.anchoredPosition+=new Vector2(360,-75);
            return text;
        }
        // Deterministic decorative particles: never consume the card/deck random stream.
        private List<RectTransform> ImpactParticles(Transform parent,int count,Color color)
        {
            var particles=new List<RectTransform>(count);
            for(int i=0;i<count;i++)particles.Add(Box(parent,"Impact Particle",716,330,i%3==0?26:8,i%3==0?4:10,i%4==0?ink:color,false));
            return particles;
        }
        private void AnimateParticles(List<RectTransform> particles,float t,bool falling=false)
        {
            for(int i=0;i<particles.Count;i++)
            {
                float angle=i*2.399963f, distance=(110+(i%7)*39)*t;
                var p=particles[i];
                p.anchoredPosition=new Vector2(716+Mathf.Cos(angle)*distance,-330+Mathf.Sin(angle)*distance*.62f-(falling?180*t*t:60*t*t));
                p.localEulerAngles=new Vector3(0,0,i*37+t*(i%2==0?260:-260));
                p.localScale=Vector3.one*Mathf.Max(0,1-t*t);
                var image=p.GetComponent<Image>();var c=image.color;c.a=1-t*t;image.color=c;
            }
        }
        private void PlayOutcomeSound(bool bust,int tier=0)
        {
            if(fastForward||muted)return;
            var cached=bust?crashSound:jackpotSound;
            if(cached==null)
            {
                const int rate=22050;float duration=bust?.85f:1.05f;
                var samples=new float[Mathf.CeilToInt(rate*duration)];
                int[] chord={0,4,7,12,16,19};
                double phase=0;
                for(int i=0;i<samples.Length;i++)
                {
                    float t=(float)i/rate,env=Mathf.Min(1,t/.006f)*Mathf.Pow(1-t/duration,2);
                    // Falling dissonance + deterministic metallic transient, or a rising major arpeggio.
                    float hz=bust?210-145*t/duration:440*Mathf.Pow(2,chord[Mathf.Min(5,(int)(t*10))]/12f);
                    phase+=2*System.Math.PI*hz/rate;
                    float wave=(float)System.Math.Sin(phase)*.42f+(float)System.Math.Sin(phase*(bust?1.059f:1.5f))*.18f;
                    wave+=Mathf.Sin(2*Mathf.PI*(bust?52:82)*t)*Mathf.Exp(-7*t)*.28f;
                    if(bust)wave+=Mathf.Sin(i*1.713f)*Mathf.Sin(i*.317f)*Mathf.Exp(-15*t)*.16f;
                    samples[i]=wave*env;
                }
                cached=AudioClip.Create(bust?"Bust collapse":"Jackpot fanfare",samples.Length,1,rate,false);
                cached.SetData(samples,0);fxSounds.Add(cached);
                if(bust)crashSound=cached;else jackpotSound=cached;
            }
            audioSource.PlayOneShot(cached,bust?1f:.65f+tier*.12f);
        }
        private IEnumerator ScoreStep(float from,float to,string caption,int step)
        {
            if(fastForward)yield break;
            var callout=ImpactTitle(effects,"Multiplier Pop",170,caption,28+step*3,step<2?mint:gold);
            var group=callout.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;
            yield return Beat(.19f+step*.025f,t=>{
                Write("Playing Table/Score Reward/Amount","+"+Mathf.RoundToInt(Mathf.Lerp(from,to,1-Mathf.Pow(1-t,2))).ToString("N0")+"점");
                callout.transform.localScale=Vector3.one*(1+.28f*Mathf.Sin(t*Mathf.PI));
                group.alpha=Mathf.Min(1,(1-t)*5);
            });
            Destroy(callout.gameObject);
        }
        private AudioClip RewardTone(string title,float hz,float seconds,bool impact=false)
        {
            const int rate=22050;
            var samples=new float[Mathf.CeilToInt(seconds*rate)];
            for(int i=0;i<samples.Length;i++)
            {
                float t=(float)i/rate, fade=Mathf.Min(1,t/.008f)*Mathf.Pow(1-t/seconds,2);
                float wave=impact?Mathf.Sin(2*Mathf.PI*(85*t-28*t*t))*.65f+Mathf.Sin(2*Mathf.PI*880*t)*Mathf.Exp(-9*t)*.2f:
                    Mathf.Sin(2*Mathf.PI*hz*t)*.45f+Mathf.Sin(2*Mathf.PI*hz*1.5f*t)*.23f+Mathf.Sin(2*Mathf.PI*hz*2.03f*t)*.12f;
                samples[i]=wave*fade;
            }
            var clip=AudioClip.Create(title,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        private void ImpactSound()
        {
            if(impactSound==null){impactSound=RewardTone("Score impact",60,.48f,true);fxSounds.Add(impactSound);}
            if(!fastForward)Play(impactSound);
        }
        private IEnumerator ScoreImpact(bool record)
        {
            int tier=Run.lastPoints>=Run.Target?3:Run.lastPoints>=Run.Target*.4f?2:Run.lastPoints>=Run.Target*.15f?1:0;
            if(record)tier=Mathf.Max(1,tier);
            Color accent=tier>=2?gold:mint;
            // Frame the table while keeping its cards and score readable.
            var focus=Rect(effects,"Score Focus",0,0,1600,900);
            var group=focus.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;
            Box(focus,"Left Shade",0,0,290,840,new Color(0,0,0,.6f));
            Box(focus,"Right Shade",1142,0,458,840,new Color(0,0,0,.6f));
            Box(focus,"Top Shade",290,0,852,130,new Color(0,0,0,.5f));
            yield return Beat(.1f,t=>group.alpha=t);
            var trails=new List<RectTransform>();
            for(int i=0;i<Run.hand.Count;i++)trails.Add(Box(focus,"Gathered Light",0,0,8,8,gold));
            yield return Beat(.22f,t=>{
                for(int i=0;i<trails.Count;i++){
                    Vector2 from=new Vector2(716+(i-(trails.Count-1)/2f)*120,565);
                    var at=Vector2.Lerp(from,new Vector2(716,324),t*t);
                    trails[i].anchoredPosition=new Vector2(at.x,-at.y);
                }
            });
            foreach(var trail in trails)Destroy(trail.gameObject);
            if(!fastForward)audioSource.Stop();
            yield return Beat(.09f);
            FocusImpact();
            var particles=ImpactParticles(focus,24+tier*16,accent);
            var flash=Box(focus,"Impact Glow",296,134,840,704,new Color(accent.r,accent.g,accent.b,0),false).GetComponent<Image>();
            var headline=ImpactTitle(focus,"Score Hype",174,new[]{"NICE!","BIG SCORE!","MEGA SCORE!","JACKPOT!!!"}[tier],34+tier*5,accent);
            var score=ImpactTitle(focus,"Score Slam",267,"+"+Run.lastPoints.ToString("N0"),100+tier*8,ink);
            Text(focus,"Score Detail",396,424,640,35,record?"NEW BEST  /  이번 런 최고 기록!":"합계 "+Run.lastNumber+"  ·  "+Run.streak+"연속 성공",21,accent,TextAnchor.MiddleCenter);
            ImpactSound();PlayOutcomeSound(false,tier);
            yield return Beat(.64f+tier*.09f,t=>{
                float bounce=Mathf.Sin(t*Mathf.PI*4)*Mathf.Exp(-7*t);
                score.transform.localScale=new Vector3(1+Mathf.Pow(1-t,8)*.8f+bounce*.12f,1-bounce*.18f,1);
                headline.transform.localScale=Vector3.one*(1+.16f*Mathf.Sin(t*Mathf.PI));
                flash.color=new Color(accent.r,accent.g,accent.b,.2f*Mathf.Pow(1-t,6));
                AnimateParticles(particles,t);ShakeImpact(t,6+tier*3);
            });
            yield return Beat(.16f,t=>group.alpha=1-t);
            RestoreImpact();
            Destroy(focus.gameObject);
        }
        private IEnumerator BustImpact(int beforeScore)
        {
            if(fastForward)yield break;
            var root=Rect(effects,"Bust Impact",0,0,1600,900);
            var group=root.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;
            Box(root,"Bust Shade",296,134,840,704,new Color(.07f,.005f,.015f,.88f),false);
            FocusImpact();audioSource.Stop();
            var title=ImpactTitle(root,"Bust Stamp",229,"…",116,red);
            yield return Beat(.14f);
            title.text="BUST!!";PlayOutcomeSound(true);
            var particles=ImpactParticles(root,46,red);
            // Broken-card silhouettes fall away underneath the verdict.
            var shards=new List<RectTransform>();
            for(int i=0;i<Mathf.Min(Run.hand.Count,8);i++)
            {
                for(int side=0;side<2;side++)
                {
                    var shard=Box(root,"Broken Card",0,0,38,100,side==0?ink:red);
                    shard.gameObject.AddComponent<CanvasGroup>().blocksRaycasts=false;
                    if(side==0)Text(shard,"Rank",3,5,32,45,Run.hand[i].Label,25,bg,TextAnchor.MiddleCenter);
                    shards.Add(shard);
                }
            }
            // Keep the verdict and actual loss above the debris.
            title.transform.SetAsLastSibling();
            var loss=ImpactTitle(root,"Bust Loss",345,Run.lastPoints<0?Run.lastPoints.ToString("N0")+"점":"0점 · 점수 하한 보호",48,ink);
            Text(root,"Streak Broken",396,491,640,34,Run.lastStreak>0?Run.lastStreak+"연속 성공 끊김!":"한 장이 너무 많았다…",23,red,TextAnchor.MiddleCenter);
            var cut=Box(root,"Bust Slash",336,325,760,5,red,false);cut.localEulerAngles=new Vector3(0,0,-7);
            Write("Playing Table/Score Reward/Amount",Run.lastPoints.ToString("N0")+"점");
            yield return Beat(.78f,t=>{
                title.transform.localScale=Vector3.one*(1+.65f*Mathf.Pow(1-t,9));
                title.rectTransform.localEulerAngles=new Vector3(0,0,Mathf.Sin(t*55)*4*(1-t)*(1-t));
                loss.transform.localScale=Vector3.one*(1+.12f*Mathf.Sin(t*Mathf.PI));
                ShakeImpact(t,14);AnimateParticles(particles,t,true);
                for(int i=0;i<shards.Count;i++)
                {
                    float side=i%2==0?-1:1;
                    shards[i].anchoredPosition=new Vector2(716+(i/2-(shards.Count/2-1)/2f)*76+side*(4+65*t),-560+45*t-180*t*t);
                    shards[i].localEulerAngles=new Vector3(0,0,side*t*65);
                    shards[i].GetComponent<CanvasGroup>().alpha=1-t;
                }
            });
            Text(root,"Bust Recovery",396,556,640,35,beforeScore==0?"점수는 0 아래로 떨어지지 않아요":"다음 핸드에서 되찾자.",20,dim,TextAnchor.MiddleCenter);
            yield return Beat(.24f);
            yield return Beat(.18f,t=>group.alpha=1-t);
            RestoreImpact();Destroy(root.gameObject);
        }
        private IEnumerator GoalImpact()
        {
            ImpactSound();
            yield return Spark(new Vector2(151,435),gold,18);
            var border=Rect(effects,"Goal Border",0,0,1600,900);
            var cg=border.gameObject.AddComponent<CanvasGroup>();cg.blocksRaycasts=false;
            var top=Box(border,"Top",296,134,0,4,gold);
            var bottom=Box(border,"Bottom",1136,834,0,4,gold);
            var left=Box(border,"Left",296,134,4,0,gold);
            var right=Box(border,"Right",1132,134,4,0,gold);
            yield return Beat(.2f,t=>{top.sizeDelta=new Vector2(840*t,4);bottom.sizeDelta=new Vector2(840*t,4);bottom.anchoredPosition=new Vector2(1136-840*t,-834);left.sizeDelta=right.sizeDelta=new Vector2(4,704*t);});
            var stamp=Text(border,"Goal Stamp",466,179,500,60,"목 표 달 성",40,gold,TextAnchor.MiddleCenter);
            yield return Beat(.3f,t=>stamp.transform.localScale=Vector3.one*(1+.3f*(1-t)*(1-t)));
            yield return Beat(.16f,t=>cg.alpha=1-t);
            Destroy(border.gameObject);
        }
        private IEnumerator CoinStream(int from,int to,Vector2 source)
        {
            int count=Mathf.Min(Mathf.Abs(to-from),8),arrived=0;
            var tokens=new List<RectTransform>();
            Vector2 wallet=new Vector2(1488,57),start=to>from?source:wallet,end=to>from?wallet:source;
            for(int i=0;i<count;i++){
                var token=Box(effects,"Coin",start.x,start.y,24,24,gold);
                Text(token,"Mark",0,0,24,24,"•",18,bg,TextAnchor.MiddleCenter);tokens.Add(token);
            }
            if(coinTick==null){coinTick=RewardTone("Coin tick",1450,.065f);fxSounds.Add(coinTick);}
            var walletView=page.Find("Wallet");
            float duration=.3f+(count-1)*.045f;
            yield return Beat(duration,t=>{
                int landed=0;
                for(int i=0;i<count;i++){
                    float progress=Mathf.Clamp01((t*duration-i*.045f)/.3f);
                    var at=Vector2.Lerp(start,end,progress)+new Vector2(Mathf.Sin(progress*Mathf.PI)*(i%2==0?22:-22),-Mathf.Sin(progress*Mathf.PI)*80);
                    tokens[i].anchoredPosition=new Vector2(at.x-12,-at.y+12);
                    tokens[i].gameObject.SetActive(progress<1);
                    if(progress>=1)landed++;
                }
                if(landed>arrived){arrived=landed;if(!fastForward)Play(coinTick);}
                Write("WalletValue",Mathf.RoundToInt(Mathf.Lerp(from,to,(float)arrived/count))+" 코인");
                if(walletView!=null)walletView.localScale=Vector3.one*(1+.045f*Mathf.Abs(Mathf.Sin(t*count*Mathf.PI)));
            });
            if(walletView!=null)walletView.localScale=Vector3.one;
            Write("WalletValue",to+" 코인");
            foreach(var token in tokens)Destroy(token.gameObject);
        }
    }
}
