using System;
using NumberTable;
using UnityEngine;
using UnityEditor;
public static class TableBalanceAudit {
 [MenuItem("Tools/Number Table/Historical 0.2 Shop Balance (Not Dealer)")]
 public static void MenuReport() { Debug.Log(Report()); }
 public static string Report(int samples=200,int firstSeed=110001) {
 var report=new System.Text.StringBuilder();
 foreach(bool baseline in new[]{true,false}) {
 var rules=new TableRules();
 if(baseline) {
 rules.numberBaseScores=null;rules.removeMaxPrice=int.MaxValue;
 JsonUtility.FromJsonOverwrite("{\n  \"stageTargets\": [1200, 2000, 3100, 4500, 6500],\n  \"handsPerStage\": 8,\n  \"shopEvery\": 2,\n  \"initialCoins\": 12,\n  \"successCoins\": 4,\n  \"bustCoins\": 1,\n  \"stageCoins\": 8,\n  \"coinsPerRemainingHand\": 1,\n  \"minDeck\": 16,\n  \"maxDeck\": 40,\n  \"rankLimit\": 6,\n  \"aceLimit\": 4,\n  \"addPrice\": 4,\n  \"acePrice\": 8,\n  \"removePrice\": 5,\n  \"removeIncrease\": 1,\n  \"levelPrice\": 6,\n  \"levelIncrease\": 4,\n  \"unlockPrice\": 8,\n  \"bustTargetFraction\": 0.12,\n  \"scoreScale\": 10.0,\n  \"levelMultipliers\": [1.0, 1.3, 1.6, 2.0, 2.5],\n  \"rarityMultipliers\": [1.0, 1.2, 1.4, 1.8, 2.2],\n  \"streakBonuses\": [0.0, 0.1, 0.2, 0.35, 0.5, 0.7],\n  \"rarity\": [5,4,4,3,3,2,2,2,1,1,1,1,2,2,3,3,3,3,3,3,3,3,4,4,4,4,4,4,4,4,5]\n}\n",rules);
 } else {var asset=Resources.Load<TextAsset>("TableBalance");if(asset!=null)JsonUtility.FromJsonOverwrite(asset.text,rules);}
 foreach(int target in new[]{3,20,33}) {
 int wins=0,stageSum=0,hitsSum=0,bustSum=0;long pointsSum=0,targetSum=0;
 for(int seed=firstSeed;seed<firstSeed+samples;seed++) {
 var g=new TableRun(rules);g.Reset(seed);
 var outcome=Play(g,target);
 if(g.phase==RunPhase.Victory)wins++;
 stageSum+=g.stage+1;hitsSum+=outcome[0];targetSum+=outcome[1];pointsSum+=g.totalScore;bustSum+=g.totalBusts;
 }
 report.AppendLine((baseline?"Before":"After")+" target="+target+" wins="+wins+"/"+samples+" avgStage="+((double)stageSum/samples).ToString("0.00")+" targetHits="+((double)hitsSum/samples).ToString("0.00")+" targetScoreShare="+((double)targetSum/Math.Max(1,pointsSum)).ToString("0.00")+" busts="+((double)bustSum/samples).ToString("0.00"));
 }
 }
 return report.ToString();
 }
 // Same paired seeds and three-draw lookahead; rank counts only, never shoe order.
 // Purchases use normal transactions. Only the target number is upgraded, except 20's secondary21.
 public static int[] Play(TableRun g,int target) {
 var log=new System.Text.StringBuilder();int targetHits=0,targetPoints=0,steps=0;
 while(g.phase!=NumberTable.RunPhase.Victory&&g.phase!=NumberTable.RunPhase.Defeat&&steps++<300)
{
 if(g.phase==NumberTable.RunPhase.Workshop)
 {
  if(target==33 && !g.unlocked[33])g.Unlock(33);
  if(g.unlocked[target]) {
   if(g.levels[target]<2)g.Upgrade(target);
   if(target==20) {
    while(g.CanUpgrade(20))g.Upgrade(20);
    foreach(int r in new[]{10,11,12,13})while(g.CanAdd(r))g.Add(r);
    while(g.CanUpgrade(21))g.Upgrade(21);
   } else {
    int limit=g.stage==0?4:g.rules.aceLimit;
    while(g.RankCount(1)<limit&&g.CanAdd(1))g.Add(1);
    if(target==3)while(g.RankCount(2)<limit&&g.CanAdd(2))g.Add(2);
    if(g.stage>=1)while(g.CanUpgrade(target))g.Upgrade(target);
    foreach(int r in new[]{13,12,11,10,9,8,7,6,5,4,3}) {
     if(target==33&&(r==5||r==6||r==3))continue;
     while(g.CanRemove(r))g.Remove(r);
    }
   }
  }
  log.AppendLine("Shop stage"+(g.stage+1)+" targetLevel="+g.levels[target]+" unlocked="+g.unlocked[target]+" deck="+g.deck.Count+" aces="+g.RankCount(1)+" coins="+g.coins);
  g.LeaveWorkshop();
 }
 else if(g.phase==NumberTable.RunPhase.Playing)
 {
  // Compare all possible totals, choosing Ace values through the game's public actions.
  var aceIndices=new System.Collections.Generic.List<int>();int low=0;
  for(int i=0;i<g.hand.Count;i++){if(g.hand[i].rank==1)aceIndices.Add(i);low+=g.hand[i].Value;}
  int bestHigh=0;int bestScore=-1;
  for(int h=0;h<=aceIndices.Count;h++) {int n=low+10*h;int score=g.Score(n);if(n==target&&g.unlocked[target])score+=10000;if(score>bestScore){bestScore=score;bestHigh=h;}}
  // AUTO is safe and can bridge multi-Ace choices without forcing an invalid intermediate value.
  foreach(int a in aceIndices)g.SetAce(a,0);
  for(int i=0;i<aceIndices.Count;i++)g.SetAce(aceIndices[i],i<bestHigh?11:1);
  int stand=g.Score(g.Current.total);
  // Exact rank counts only: never inspect or exploit draw order.
  var available=g.PossibleNext();var counts=new int[11];foreach(var c in available)counts[c.Value]++;
  System.Func<int,int,int> value=(baseSum,aces)=> {int best=0;for(int h=0;h<=aces;h++)best=System.Math.Max(best,g.Score(baseSum+10*h));return best;};
  System.Func<int,int,int,int,double> ev=null;
  ev=(baseSum,aces,depth,left)=>{
   int score=value(baseSum,aces);if(score==0)return -System.Math.Min(g.stageScore,g.Penalty);
   if(depth==0||left==0)return score;
   double draw=0;for(int r=1;r<=10;r++)if(counts[r]>0){int copies=counts[r];counts[r]--;draw+=(double)copies/left*ev(baseSum+r,aces+(r==1?1:0),depth-1,left-1);counts[r]++;}
   return System.Math.Max(score,draw);
  };
  double hitEv=0;int remaining=available.Count;
  for(int r=1;r<=10;r++)if(counts[r]>0){int copies=counts[r];counts[r]--;hitEv+=(double)copies/remaining*ev(low+r,aceIndices.Count+(r==1?1:0),2,remaining-1);counts[r]++;}
  bool hit=hitEv>stand;
  if(g.Current.total==target||g.stageScore+stand>=g.Target)hit=false;
  else if(g.HandsLeft==0&&g.stageScore+stand<g.Target)hit=true;
  if(hit)g.Hit();else g.Stand();
 }
 else if(g.phase==NumberTable.RunPhase.Result)
 {
  if(!g.lastBust&&g.lastNumber==target){targetHits++;targetPoints+=g.lastPoints;}
  log.AppendLine("Stage"+(g.stage+1)+" hand"+g.handsPlayed+" number="+g.lastNumber+" points="+g.lastPoints+" stageScore="+g.stageScore+"/"+g.Target+" coins="+g.coins);
  g.Advance();
 }
 else if(g.phase==NumberTable.RunPhase.StageClear)g.Advance();
}

 if(steps>=300)throw new Exception("Policy stalled");
 return new[]{targetHits,targetPoints};
 }
}
