using System.Collections;
using System.Linq;
using UnityEngine;

namespace NumberTable
{
    public partial class TableView
    {
        private int stampChoice=-1,stampAnchor=-1;
        private StampOffer selectedStamp;
        private int[] stampedCells=new int[0];
        private int stampedKey=-1;
        private void EnsureStampSelection()
        {
            if(Run.phase!=RunPhase.Stamp)return;
            if(stampChoice>=0&&stampChoice<Run.stamps.Count&&ReferenceEquals(selectedStamp,Run.stamps[stampChoice]))return;
            stampChoice=0;selectedStamp=Run.stamps[0];stampAnchor=Run.FirstStampAnchor(0);
        }
        public void SelectStamp(int index)
        {
            if(IsPresenting||Run.phase!=RunPhase.Stamp||index<0||index>=Run.stamps.Count)return;
            stampChoice=index;selectedStamp=Run.stamps[index];stampAnchor=Run.FirstStampAnchor(index);Play(clickSound);Render();
        }
        public void ConfirmStamp()
        {
            if(IsPresenting||Run.phase!=RunPhase.Stamp||!Run.CanStamp(selectedStamp,stampAnchor))return;
            stampedCells=Run.StampCells(selectedStamp,stampAnchor);
            stampedKey=selectedStamp.IsKey?stampedCells[selectedStamp.keyCell]:-1;
            int choice=stampChoice,anchor=stampAnchor;
            Present(()=>Run.ApplyStamp(choice,anchor),"stamp");
        }
        public void SkipStampChoice(){if(IsPresenting)return;Run.SkipStamp();Render();}
        private void StampWorkshop()
        {
            var p=Box(page,"Stamp Workshop",296,134,840,704,Hex("203B37"));
            Text(p,"Step",30,24,780,25,"01  숫자판 강화     →     02  카드 정비",14,gold);
            Text(p,"Title",30,66,780,52,"모양을 골라, 숫자를 키워라.",32,ink);
            Text(p,"Instructions",30,128,780,56,"블록 1개 선택 → 오른쪽 숫자를 눌러 위치 지정 → 찍기\n회전 없음 · 선택한 숫자가 블록 틀의 왼쪽 위 기준점입니다.",16,dim);
            for(int i=0;i<Run.stamps.Count;i++)
            {
                int index=i;var offer=Run.stamps[i];bool active=i==stampChoice;
                var card=Button(p,"Stamp Candidate "+i,30+i*262,206,250,228,"",()=>SelectStamp(index),active?Hex("40594B"):panel,ink);
                Text(card,"Name",12,12,226,32,TableRun.StampName(offer.shape)+"  "+(offer.IsKey?"열쇠 블록":"성장 블록"),21,active?gold:ink,TextAnchor.MiddleCenter);
                for(int cell=0;cell<4;cell++)
                {
                    bool key=cell==offer.keyCell;
                    var tile=Box(card,"Shape Cell",40+TableRun.StampX(offer.shape,cell)*42,58+TableRun.StampY(offer.shape,cell)*42,38,38,key?gold:mint);
                    Text(tile,"Effect",0,0,38,38,key?"열쇠":"+1",key?10:17,bg,TextAnchor.MiddleCenter);
                }
                Text(card,"Caption",8,194,234,24,offer.IsKey?"열쇠 칸 해금 · 열린 주변 칸 +1":"열린 칸 레벨 +1 · 잠긴 칸 유지",12,dim,TextAnchor.MiddleCenter);
            }
            bool valid=Run.CanStamp(selectedStamp,stampAnchor);
            int[] cells=Run.StampCells(selectedStamp,stampAnchor);
            if(cells.Length==0)Text(p,"Preview",30,457,780,72,"판 밖이나 마지막 행의 빈칸에는 놓을 수 없어요.",15,red,TextAnchor.MiddleCenter);
            else for(int i=0;i<cells.Length;i++)Text(p,"Preview "+i,30+i*195,457,190,72,StampPreview(cells[i],i),14,valid?mint:red,TextAnchor.MiddleCenter);
            Text(p,"Placement Rule",30,532,780,34,valid?"미리보기입니다. 아래 ‘찍기’를 눌러야 적용돼요.":selectedStamp.IsKey?"열쇠 칸을 아직 잠긴 숫자에 맞춰주세요.":"강화할 수 있는 열린 숫자가 최소 한 칸 필요해요.",14,valid?dim:red,TextAnchor.MiddleCenter);
            Button(p,"Confirm Stamp",164,586,510,54,"이 위치에 찍기!  →  카드 정비",ConfirmStamp,gold,bg,21,valid);
            Button(p,"Skip Stamp",290,652,260,30,"이번 블록은 넘기기",SkipStampChoice,panel,dim,13);
        }
        private string StampPreview(int n,int cell)
        {
            if(cell==selectedStamp.keyCell&&!Run.unlocked[n])
            {
                int score=(int)System.Math.Round(Run.BaseScore(n)*Run.LevelMult(n)*Run.RarityMult(n)*Run.StreakMult,System.MidpointRounding.AwayFromZero);
                return n+" 해금!\n0 → "+score.ToString("N0")+"점";
            }
            if(!Run.unlocked[n])return n+" 잠김\n변화 없음";
            if(Run.levels[n]>=Run.rules.levelMultipliers.Length||cell==selectedStamp.keyCell)return n+"\n변화 없음";
            return n+" Lv."+Run.levels[n]+" → "+(Run.levels[n]+1)+"\n"+Run.Score(n).ToString("N0")+" → "+Run.Score(n,-1,Run.levels[n]+1).ToString("N0");
        }
        private IEnumerator StampImpact()
        {
            for(int i=0;i<stampedCells.Length;i++)
            {
                int n=stampedCells[i];Note(i);
                yield return Pulse(page.Find("Number Collection/Number "+n),.25f);
            }
            ImpactSound();
            var label=Text(effects,"Stamp Complete",436,352,560,80,stampedKey>=0?stampedKey+" 해금!  +  숫자판 강화":"숫자판 강화 완료!",34,gold,TextAnchor.MiddleCenter);
            yield return Spark(NumberPosition(stampedCells[0]),gold,22);
            yield return Beat(.25f);Destroy(label.gameObject);
        }
    }
}
