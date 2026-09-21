using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace NumberTable
{
    public partial class TableView : MonoBehaviour
    {
        public TableRun Run { get; private set; }
        public string LastAction { get; private set; }
        private Font uiFont, displayFont;
        private Sprite rounded;
        private Transform canvas, page;
        private AudioSource audioSource;
        private AudioClip clickSound, cardSound, winSound, bustSound;
        private bool muted, showHelp, showDeck, showLab;
        private string labReport;
        private readonly Color bg=Hex("101C1F"), panel=Hex("18272A"), line=Hex("304246"), ink=Hex("EEF0E8"), dim=Hex("90A3A2"), gold=Hex("E6C581"), mint=Hex("9FCAB7"), red=Hex("E48170");
        private readonly string[] rooms={ "THE LOBBY", "PRIVATE ROOM", "HIGH ROLLER", "BACK ROOM", "THE TABLE" };
        public static TableView Instance;

        private void Awake()
        {
            Instance=this;
            Application.targetFrameRate=60;
            var rules=new TableRules(); var json=Resources.Load<TextAsset>("TableBalance");
            if(json!=null) JsonUtility.FromJsonOverwrite(json.text,rules);
            Run=new TableRun(rules);
            uiFont=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial","Liberation Sans"},24);
            displayFont=Font.CreateDynamicFontFromOSFont(new[]{"Georgia","Times New Roman","Arial"},64);
            if(uiFont==null) uiFont=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if(displayFont==null) displayFont=uiFont;
            rounded=MakeRounded();
            var c=new GameObject("Table Canvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            c.transform.SetParent(transform,false); var cv=c.GetComponent<Canvas>(); cv.renderMode=RenderMode.ScreenSpaceOverlay;
            var scale=c.GetComponent<CanvasScaler>(); scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution=new Vector2(1600,900); scale.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            canvas=c.transform;
            if(FindFirstObjectByType<EventSystem>()==null)
            {
                var es=new GameObject("Table Input",typeof(EventSystem),typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform,false); es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            audioSource=gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake=false; audioSource.volume=0.25f;
            clickSound=Tone("button",350,0.055f); cardSound=Tone("card",660,0.085f); winSound=Tone("stand",880,0.22f); bustSound=Tone("bust",105,0.3f);
            Render();
        }

        private void Update()
        {
            var k=Keyboard.current; if(k==null) return;
            if(IsPresenting) { if(k.spaceKey.wasPressedThisFrame||k.escapeKey.wasPressedThisFrame) SkipPresentation(); return; }
            if(k.escapeKey.wasPressedThisFrame && (showHelp||showDeck||showLab)) { showHelp=showDeck=showLab=false; Render(); return; }
            if(showHelp||showDeck||showLab) return;
            if(Run.phase==RunPhase.Playing)
            {
                if(k.hKey.wasPressedThisFrame) ActHit();
                else if(k.sKey.wasPressedThisFrame) ActStand();
            }
            else if(k.spaceKey.wasPressedThisFrame)
            {
                if(Run.phase==RunPhase.Welcome) NewRun();
                else if(Run.phase==RunPhase.Workshop) StartHand();
                else if(Run.phase==RunPhase.Result||Run.phase==RunPhase.StageClear) Continue();
            }
        }

        public void NewRun() { CancelPresentation(); bestHand=0; Run.Reset(Environment.TickCount & 0x7fffffff); showHelp=showDeck=showLab=false; LastAction="new"; Play(clickSound); Render(); }
        public void StartHand() { if(IsPresenting||Run.phase!=RunPhase.Workshop)return; Present(Run.LeaveWorkshop,"deal",true); }
        public void ActHit() { if(IsPresenting||Run.phase!=RunPhase.Playing||Run.NeedsAceChoice)return; Present(Run.Hit,"hit",true); }
        public void ActStand() { if(IsPresenting||Run.phase!=RunPhase.Playing||Run.NeedsAceChoice)return; Present(Run.Stand,"stand"); }
        public void Continue() { if(IsPresenting)return; bool draw=Run.phase==RunPhase.Result&&Run.stageScore<Run.Target&&Run.HandsLeft>0&&Run.handsPlayed%Run.rules.shopEvery!=0; Present(Run.Advance,"continue",draw); }
        public void SelectNumber(int n) { if(IsPresenting)return; Run.selected=n; if(Run.phase==RunPhase.Stamp)stampAnchor=n; Play(clickSound); Render(); }
        private void ChooseAce(int index,int choice) { if(IsPresenting)return; Present(()=>Run.SetAce(index,choice),"ace"); }

        private void Play(AudioClip clip) { if(!muted && clip!=null) audioSource.PlayOneShot(clip); }
        public void Render(bool animate=false)
        {
            if(page!=null) { page.gameObject.SetActive(false); Destroy(page.gameObject); }
            page=Box(canvas,"Page",0,0,1600,900,bg,false);
            var pageRect=(RectTransform)page;
            pageRect.anchorMin=pageRect.anchorMax=pageRect.pivot=new Vector2(.5f,.5f);
            pageRect.anchoredPosition=Vector2.zero;
            EnsureStampSelection();Header(); Sidebar(); Numbers();
            if(Run.phase==RunPhase.Welcome) Welcome();
            else if(Run.phase==RunPhase.Stamp) StampWorkshop();
            else if(Run.phase==RunPhase.Workshop) Workshop();
            else if(Run.phase==RunPhase.StageClear||Run.phase==RunPhase.Victory||Run.phase==RunPhase.Defeat) EndPanel();
            else Table(animate);
            Text(page,"Footer",34,864,900,22,"BLACKJACK, REWRITTEN.     /     DEALER PROTOTYPE  0.3",11,dim);
            Text(page,"Shortcuts",1140,864,426,22,"H  HIT     S  STAND     SPACE  진행",12,dim,TextAnchor.MiddleRight);
            if(showHelp) HelpModal();
            if(showDeck) DeckModal();
            if(showLab) LabModal();
            var input=page.gameObject.AddComponent<CanvasGroup>(); input.interactable=!IsPresenting; input.blocksRaycasts=!IsPresenting;
            if(effects!=null)effects.SetAsLastSibling();
        }
        private void Header()
        {
            Text(page,"Brand",34,24,440,49,"THE NUMBER TABLE",30,ink,TextAnchor.MiddleLeft,true);
            Text(page,"Subbrand",36,73,520,22,"숫자를 키우고. 덱을 설계하고. 한 장 더.",13,dim);
            Button(page,"Guide",972,37,100,39,"플레이 가이드",()=>{showHelp=true;Render();},panel,ink,12);
            Button(page,"Deck",1084,37,113,39,"덱 보기  "+Run.deck.Count,()=>{showDeck=true;Render();},panel,ink,13);
            Button(page,"Lab",1209,37,100,39,"딜러 규칙",()=>{showHelp=true;Render();},panel,ink,12);
            Button(page,"Sound",1321,37,73,39,muted?"음소거":"소리 켬",()=>{muted=!muted;Render();},panel,dim,12);
            Box(page,"Wallet",1410,30,156,54,gold);
            Text(page,"WalletCaption",1420,34,136,18,"보유 코인",11,bg,TextAnchor.MiddleCenter);
            Text(page,"WalletValue",1420,51,136,29,(visualCoins??Run.coins)+" 코인",21,bg,TextAnchor.MiddleCenter);
            Box(page,"HeaderRule",34,109,1532,1,line,false);
        }
        private void Sidebar()
        {
            var p=Box(page,"Run Status",34,134,240,704,panel);
            Text(p,"StageCaption",22,23,195,20,"YOUR RUN",11,dim);
            Text(p,"StageNumber",22,60,180,65,(Run.stage+1).ToString("00")+" / 05",39,ink,TextAnchor.MiddleLeft,true);
            Text(p,"RoomName",23,126,200,24,rooms[Run.stage],13,gold);
            Box(p,"rule",22,170,196,1,line,false);
            Text(p,"TargetCaption",22,194,194,24,"스테이지 점수",13,dim);
            Text(p,"Score",21,229,205,49,(visualScore??Run.stageScore).ToString("N0"),35,ink,TextAnchor.MiddleLeft,true);
            Text(p,"Goal",22,281,196,23,"/ "+Run.Target.ToString("N0")+" 목표",14,dim);
            Box(p,"ProgressBg",22,322,196,6,line);
            Box(p,"Progress",22,322,196*Mathf.Clamp01((float)(visualScore??Run.stageScore)/Run.Target),6,mint);
            Text(p,"Hands",22,351,197,30,"남은 핸드    "+Run.HandsLeft+" / "+(Run.rules.handsPerStage+Run.bonusHands),15,ink);
            Text(p,"ShopTiming",22,389,197,25,"2핸드마다 덱 정비",12,dim);
            Box(p,"StreakBox",18,449,204,91,Hex("283733"));
            Text(p,"Streak",33,461,172,31,Run.streak+"  STREAK",21,mint,TextAnchor.MiddleLeft,true);
            Text(p,"StreakBonus",33,501,172,22,"다음 성공 점수 ×"+Run.StreakMult.ToString("0.00"),12,mint);
            Text(p,"CoinRules",22,550,196,85,"매 핸드 성공  +"+Run.rules.successCoins+"코인\n버스트해도  +"+Run.rules.bustCoins+"코인\n클리어 기본  +"+Run.rules.stageCoins+"코인\n남은 핸드당 추가 +"+Run.rules.coinsPerRemainingHand+"코인",12,gold);
            Text(p,"BustCaption",22,638,196,23,"버스트: −"+Run.Penalty+"점",14,red);
            Text(p,"ResetNote",22,666,196,23,"연속 성공 초기화 · 점수 하한 0",11,dim);
        }
        private void Numbers()
        {
            var p=Box(page,"Number Collection",1158,134,408,704,panel);
            Text(p,"Title",22,21,364,34,"당신의 숫자",21,ink);
            Text(p,"Caption",22,65,364,24,Run.phase==RunPhase.Stamp?"숫자를 눌러 블록 배치 · 회전 없음":"숫자를 눌러 점수 확인 · 블록으로 성장",12,dim);
            var preview=Run.phase==RunPhase.Stamp?Run.StampCells(selectedStamp,stampAnchor):new int[0];
            bool stampValid=Run.phase==RunPhase.Stamp&&Run.CanStamp(selectedStamp,stampAnchor);
            for(int n=2;n<=33;n++)
            {
                int number=n, i=n-2, col=i%5,row=i/5;
                float x=22+col*74,y=101+row*55;
                bool active=Run.selected==n, safe=Run.unlocked[n];
                var b=Button(p,"Number "+n,x,y,68,49,"",()=>SelectNumber(number),active?gold:safe?Hex("263A3D"):Hex("1D2C2F"),ink);
                Text(b,"Value",3,1,62,28,n.ToString(),22,active?bg:safe?ink:Hex("677A7D"),TextAnchor.MiddleCenter,true);
                Text(b,"Level",3,30,62,17,safe?"LV "+Run.levels[n]:"LOCKED",9,active?bg:safe?mint:Hex("677A7D"),TextAnchor.MiddleCenter);
                int cell=System.Array.IndexOf(preview,n);
                if(cell>=0)
                {
                    var color=stampValid?(cell==selectedStamp.keyCell?gold:mint):red;
                    Box(b,"Stamp Edge Top",0,0,68,3,color,false);Box(b,"Stamp Edge Bottom",0,46,68,3,color,false);
                    Box(b,"Stamp Edge Left",0,0,3,49,color,false);Box(b,"Stamp Edge Right",65,0,3,49,color,false);
                    string mark=cell==selectedStamp.keyCell?"열쇠":safe&&Run.levels[n]<Run.rules.levelMultipliers.Length?"+1":"—";
                    Text(b,"Stamp Mark",37,2,29,18,mark,10,color,TextAnchor.MiddleCenter);
                }
            }
            int selected=Run.selected; bool opened=Run.unlocked[selected];
            Box(p,"DetailRule",22,493,364,1,line,false);
            Text(p,"Selected",24,501,99,49,selected.ToString(),41,gold,TextAnchor.MiddleLeft,true);
            Text(p,"Stars",112,503,250,23,new string('★',Run.Stars(selected))+new string('☆',5-Run.Stars(selected)),17,gold);
            Text(p,"Multiplier",112,531,271,22,"기본 "+Run.BaseScore(selected)+" · Lv."+Run.levels[selected]+" ×"+Run.LevelMult(selected).ToString("0.0")+" · 희귀 ×"+Run.RarityMult(selected).ToString("0.0"),11,dim);
            Text(p,"ValuePreview",22,580,364,35,opened?"합계 "+selected+"로 멈추면  "+Run.Score(selected).ToString("N0")+"점":"이 합계는 아직 버스트입니다",19,opened?mint:dim);
            Text(p,"ShopHint",22,633,364,55,"성장 블록: 열린 칸 레벨 +1\n열쇠 블록: 열쇠 칸만 해금 · 주변 성장",12,dim);
        }

        private void Welcome()
        {
            var p=Box(page,"Welcome",296,134,840,704,Hex("203B37"));
            Text(p,"Edition",40,34,760,25,"A DIFFERENT KIND OF BLACKJACK",12,gold,TextAnchor.MiddleCenter);
            Text(p,"Hero",45,105,750,130,"21이 정답은 아니다.",47,ink,TextAnchor.MiddleCenter);
            Text(p,"HeroDesc",95,251,650,72,"18을 키웠다면, 18을 노려라.\n원하는 숫자를 위한 덱. 한 장을 더 뽑을 용기.",20,Hex("BDD0C6"),TextAnchor.MiddleCenter);
            float[] xx={232,358,484}; string[] values={"5","6","7"};
            for(int i=0;i<3;i++)
            {
                var card=Box(p,"DemoCard",xx[i],351,114,148,Hex("EEE8DB"));
                Text(card,"Rank",12,9,90,49,values[i],30,Hex("274137"),TextAnchor.UpperLeft,true);
                Text(card,"Suit",16,62,82,58,i==1?"♥":"♠",40,i==1?Hex("A84438"):Hex("274137"),TextAnchor.MiddleCenter);
            }
            Text(p,"Equation",70,523,700,25,"5 + 6 + 7 = 18     ·     YOUR NUMBER. YOUR RULES.",13,gold,TextAnchor.MiddleCenter);
            Button(p,"Begin Run",246,582,348,58,"테이블에 앉기     →",NewRun,gold,bg,20);
            Text(p,"Hint",70,656,700,24,"5개 스테이지  /  Ace 1·11 직접 선택  /  마우스 또는 H·S",12,dim,TextAnchor.MiddleCenter);
        }
        private void Table(bool animate)
        {
            var p=Box(page,"Playing Table",296,134,840,704,Hex("203B37"));
            Text(p,"HandHeader",28,22,390,26,"HAND "+Run.handsPlayed.ToString("00")+"   /   "+rooms[Run.stage],12,Hex("9EB7AC"));
            Text(p,"Phase",540,22,272,26,Run.phase==RunPhase.Playing?"MAKE YOUR NUMBER":"HAND RESOLVED",11,gold,TextAnchor.MiddleRight);
            var v=Run.Current; bool result=Run.phase==RunPhase.Result; bool acePending=Run.NeedsAceChoice;
            Text(p,"TotalCaption",150,83,540,24,acePending?"버스트 보류 · ACE 값을 선택하세요":result?(Run.lastBust?"BUST / LOCKED NUMBER":"STAND / NUMBER SECURED"):"CURRENT TOTAL",12,acePending?gold:result&&Run.lastBust?red:dim,TextAnchor.MiddleCenter);
            Text(p,"Total",160,114,520,139,v.total.ToString(),112,Run.lastBust&&result?red:ink,TextAnchor.MiddleCenter,true);
            if(acePending) Text(p,"AceNotice",40,265,760,29,"Ace 값을 바꾸면 계속할 수 있어요. 아직 점수 차감은 없어요.",16,gold,TextAnchor.MiddleCenter);
            else if(!Run.lastBust||!result)
            {
                string tags=Run.hand.Count==2&&v.total==21?"NATURAL  ·  ":"";
                Text(p,"Calculation",40,265,760,29,tags+"기본 "+Run.BaseScore(Mathf.Clamp(v.total,2,33))+" × 레벨 "+Run.LevelMult(Mathf.Clamp(v.total,2,33)).ToString("0.0")+" × 희귀도 "+Run.RarityMult(Mathf.Clamp(v.total,2,33)).ToString("0.0")+" × 연속 "+(result?(1+Run.rules.streakBonuses[Math.Min(Run.lastStreak,5)]):Run.StreakMult).ToString("0.00"),13,Hex("BBD0C6"),TextAnchor.MiddleCenter);
            }
            else Text(p,"BustLoss",40,268,760,25,"버스트 · 연속 성공이 초기화됐어요",16,red,TextAnchor.MiddleCenter);
            int coinReward=result&&Run.lastBust?Run.rules.bustCoins:Run.rules.successCoins;
            var scoreReward=Box(p,"Score Reward",100,300,310,56,Hex("192F2C"));
            Text(scoreReward,"Caption",8,4,294,18,result?"이번 핸드 점수":"지금 STAND하면",11,dim,TextAnchor.MiddleCenter);
            Text(scoreReward,"Amount",8,23,294,30,acePending?"Ace 선택 후 계산":result?(Run.lastPoints>=0?"+":"")+Run.lastPoints.ToString("N0")+"점":"+"+Run.Score(v.total).ToString("N0")+"점",24,result&&Run.lastBust?red:ink,TextAnchor.MiddleCenter);
            var coinBox=Box(p,"Coin Reward",430,300,310,56,gold);
            Text(coinBox,"Caption",8,4,294,18,result?"이번 핸드 획득 · 지급 완료":"성공 보상 · STAND 시 지급",11,bg,TextAnchor.MiddleCenter);
            Text(coinBox,"Amount",8,23,294,30,"+"+coinReward+" 코인",24,bg,TextAnchor.MiddleCenter);
            DrawHand(p,animate);
            if(!result)
            {
                Run.HitForecast(out float bust,out float ev);
                Text(p,"Risk",24,561,792,26,acePending?"카드 아래 1 / 11 / 자동을 선택해 안전한 합계를 만들어주세요":"다음 HIT 버스트  "+(bust*100).ToString("0")+"%  ·  Ace 조정 반영  ·  한 장 후 STAND 기대값  "+ev.ToString("0")+"점",12,acePending?gold:Hex("ADC8BC"),TextAnchor.MiddleCenter);
                Button(p,"HIT",148,603,260,62,"HIT    + 한 장",ActHit,gold,bg,21,!acePending);
                Button(p,"STAND",432,603,260,62,acePending?"Ace를 먼저 선택":"STAND    확정",ActStand,Hex("D9E7DF"),bg,21,!acePending);
            }
            else
            {
                string next=Run.stageScore>=Run.Target?"목표 달성! 다음으로  →":Run.HandsLeft==0?"런 결과 보기  →":Run.handsPlayed%Run.rules.shopEvery==0?"덱 정비로 이동  →":"다음 핸드 DEAL  →";
                string rewardNotice="보유 코인  "+(Run.coins-coinReward)+" → "+Run.coins+" 코인";
                if(Run.stageScore>=Run.Target) rewardNotice+="   ·   다음: 클리어 +"+Run.StageReward+"코인 (기본 "+Run.rules.stageCoins+" + 남은 핸드 "+Run.RemainingHandCoins+")";
                Text(p,"ResultNotice",35,558,770,34,rewardNotice,15,gold,TextAnchor.MiddleCenter);
                Button(p,"Next",210,610,420,56,next,Continue,gold,bg,19);
            }
            Text(p,"DeckRemaining",24,677,792,22,"DRAW PILE  "+Run.shoe.Count+" / "+Run.deck.Count+"      ·      "+(Run.phase==RunPhase.Playing?Run.notice:"SPACE로 계속"),10,Hex("9EB7AC"),TextAnchor.MiddleCenter);
        }

        private void DrawHand(Transform p,bool animate)
        {
            int count=Run.hand.Count; if(count==0) return;
            float width=Mathf.Min(110,730f/Math.Max(1,count)-8),gap=8,total=count*(width+gap)-gap,left=(840-total)/2;
            int high=Run.Current.highAutomaticAces;
            for(int i=0;i<count;i++)
            {
                int index=i; var c=Run.hand[i]; bool isRed=c.suit==1||c.suit==2; Color color=isRed?Hex("AA493E"):Hex("243C38");
                float x=left+i*(width+gap),y=365;
                Box(p,"CardShadow",x+3,y+5,width,153,Hex("142F2A"));
                var card=Box(p,"Card "+i,x,y,width,153,Hex("F0ECE1"));
                Text(card,"Rank",9,7,width-18,34,c.Label,28,color,TextAnchor.UpperLeft,true);
                string suit=new[]{"♠","♥","♦","♣"}[c.suit];
                Text(card,"Suit",4,c.rank==1?39:46,width-8,c.rank==1?48:60,suit,c.rank==1?32:39,color,TextAnchor.MiddleCenter);
                Text(card,"Corner",9,112,width-18,29,c.Label,21,color,TextAnchor.LowerRight,true);
                if(c.rank==1)
                {
                    int val=c.aceChoice==0 ? (high-->0?11:1) : c.aceChoice;
                    Text(card,"AceValue",6,91,width-12,22,"값 "+val,12,color,TextAnchor.MiddleCenter);
                    string[] labels={"자동","1","11"}; int[] values={0,1,11};
                    for(int j=0;j<3;j++)
                    {
                        int choice=values[j]; float bw=(width-4)/3;
                        Button(p,"Ace "+i+" "+choice,x+j*(bw+2),y+159,bw,29,labels[j],()=>ChooseAce(index,choice),c.aceChoice==choice?gold:Hex("314D44"),c.aceChoice==choice?bg:ink,10,Run.phase==RunPhase.Playing);
                    }
                }
                if(animate) { var motion=card.gameObject.AddComponent<CardArrival>(); motion.Setup((RectTransform)card,i*0.035f); }
            }
        }

        private void EndPanel()
        {
            bool won=Run.phase==RunPhase.Victory, lost=Run.phase==RunPhase.Defeat;
            var p=Box(page,"Stage Result",296,134,840,704,Hex("203B37"));
            Text(p,"Eyebrow",50,63,740,26,won?"THE TABLE IS YOURS":lost?"ONE RUN ENDS. A NEW BUILD BEGINS.":"A SEAT AT THE NEXT TABLE",12,lost?red:gold,TextAnchor.MiddleCenter);
            Text(p,"Headline",50,165,740,92,won?"당신의 숫자가 이겼다.":lost?"이번 덱의 끝.":"테이블 클리어.",45,ink,TextAnchor.MiddleCenter);
            Text(p,"Score",50,283,740,85,Run.stageScore.ToString("N0")+" / "+Run.Target.ToString("N0"),49,lost?red:gold,TextAnchor.MiddleCenter,true);
            Text(p,"Summary",70,407,700,79,"획득 점수  "+Run.totalScore.ToString("N0")+"     ·     HIT  "+Run.totalHits+"회     ·     BUST  "+Run.totalBusts+"회\n"+(lost?"강화할 숫자와 카드 비율을 바꿔 다시 도전해보세요.":"클리어 기본 "+Run.rules.stageCoins+" + 남은 "+Run.HandsLeft+"핸드 × "+Run.rules.coinsPerRemainingHand+" = 총 "+Run.StageReward+"코인"),16,dim,TextAnchor.MiddleCenter);
            if(!lost) Text(p,"Clear Coins",60,493,720,37,"클리어 보너스 +"+Run.StageReward+"코인 지급 완료  ·  보유 "+(Run.coins-Run.StageReward)+" → "+Run.coins+"코인",20,gold,TextAnchor.MiddleCenter);
            Button(p,"Proceed",210,553,420,63,won||lost?"새로운 런 시작   ↻":"다음 방으로   →",()=>{if(won||lost)NewRun();else Continue();},gold,bg,20);
            Text(p,"PrototypeNote",80,648,680,24,"프로토타입: 보스·이벤트는 코어 밸런스 검증 후 추가됩니다",12,dim,TextAnchor.MiddleCenter);
        }
        private Transform Modal(string title)
        {
            var shade=Box(page,"Modal Shade",0,0,1600,900,new Color(0,0,0,0.84f),false); shade.GetComponent<Image>().raycastTarget=true;
            var p=Box(shade,"Modal",260,112,1080,676,panel);
            Text(p,"Title",38,26,850,50,title,28,ink);
            Button(p,"Close",955,29,86,42,"닫기  ×",()=>{showHelp=showDeck=showLab=false;Render();},line,ink,14);
            Box(p,"Line",38,94,1004,1,line,false);
            return p;
        }
        private void HelpModal()
        {
            var p=Modal("HOW TO PLAY / 원하는 숫자를 만드는 게임");
            Text(p,"Rules",40,120,995,467,
                "01    블록으로 숫자를 키우고, 딜러에게 덱 정비\n        고정 모양 블록 1개를 찍은 뒤 카드 2장 무료 선택. 추가 선택은 2·3·4…코인.\n        뒷면 추가 공개는 별도로 1·2·3…코인. 미선택 카드는 소멸하고 다음 정비에 초기화됩니다.\n\n"+
                "02    성장 블록은 열린 칸 +1 · 열쇠 칸은 잠긴 숫자 하나 해금\n        블록은 회전 없이 배치하며, 미리보기 후 확정합니다. 두 장을 받고 HIT / STAND\n        HIT은 한 장 추가, STAND는 지금 합계로 점수 획득. Ace를 바꿔도 안전하지 않으면 BUST.\n\n"+
                "03    Ace는 당신이 결정\n        카드 아래 자동 / 1 / 11로 선택합니다. 자동은 해금된 가장 높은 안전 합계입니다.\n        HIT 후 Ace를 바꾸면 살릴 수 있을 때는 버스트를 보류합니다. 값을 고른 뒤 계속하세요.\n\n"+
                "04    점수 = 숫자별 기본 점수 × 레벨 × 고정 희귀도 × 연속 성공\n        만들기 힘든 낮은 합계에도 높은 기본 점수를 줍니다. 기본 점수는 오른쪽에서 확인하세요.\n\n"+
                "05    기본 8핸드 안에 목표 달성, 총 5개 스테이지\n        2핸드마다 정비. 핸드 +1은 이번 스테이지에만 적용됩니다. 정비 후 전체 덱을 섞습니다.",16,ink);
            Text(p,"Scope",40,602,1000,37,"MVP  ·  런 저장 없음  ·  게임 종료·새 런에서 성장 초기화  ·  카드 수 보너스 없음",13,dim);
        }
        private void DeckModal()
        {
            var p=Modal("DECK / 카드 구성");
            Text(p,"DeckExplain",40,112,995,46,"전체 덱 "+Run.deck.Count+"장 · 뽑기 더미 "+Run.shoe.Count+"장 · 현재 손패 "+Run.hand.Count+"장\nJ / Q / K = 10   ·   A = 자동 또는 직접 1 / 11 선택",14,dim);
            for(int rank=1;rank<=13;rank++)
            {
                int i=rank-1; float x=40+(i%7)*143,y=200+(i/7)*164;
                var b=Box(p,"Rank",x,y,127,140,Hex("263A3D"));
                string label=new PlayingCard(0,rank,0).Label;
                Text(b,"Label",14,10,100,57,label,39,ink,TextAnchor.MiddleLeft,true);
                Text(b,"Count",14,77,100,35,Run.RankCount(rank)+" 장",20,mint);
                Text(b,"Percent",14,114,100,21,(100f*Run.RankCount(rank)/Run.deck.Count).ToString("0.0")+"%",12,dim);
            }
            Text(p,"Info",40,569,1000,64,"표시는 전체 덱 비율입니다. HIT 위험도는 실제 남은 뽑기 더미를 사용합니다.\n희귀도는 카드 구성과 무관하게 고정됩니다. 덱 추가·제거는 딜러 카드 효과를 적용하세요.",14,dim);
        }
        private void LabModal()
        {
            var p=Modal("BALANCE LAB / 동일 예산의 세 가지 빌드");
            Text(p,"Report",40,114,1000,465,labReport??"분석 중...",16,ink);
            Text(p,"Disclaimer",40,578,1000,69,"고정 시드 · 자동 Ace · 한 장 뒤 즉시 STAND 기대값을 비교하는 단순 전략입니다.\n실제 플레이의 최적 전략이나 밸런스 확정을 뜻하지 않습니다. 모든 수치는 프로토타입용입니다.",13,dim);
        }

        private static Color Hex(string s) { ColorUtility.TryParseHtmlString("#"+s,out var c); return c; }
        private RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {
            var go=new GameObject(name,typeof(RectTransform)); var r=go.GetComponent<RectTransform>(); r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=new Vector2(0,1); r.pivot=new Vector2(0,1); r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(w,h); return r;
        }
        private RectTransform Box(Transform parent,string name,float x,float y,float w,float h,Color c,bool round=true)
        {
            var r=Rect(parent,name,x,y,w,h); var im=r.gameObject.AddComponent<Image>(); im.color=c; im.raycastTarget=false;
            if(round) { im.sprite=rounded; im.type=Image.Type.Sliced; } return r;
        }
        private Text Text(Transform parent,string name,float x,float y,float w,float h,string value,int size,Color c,TextAnchor align=TextAnchor.UpperLeft,bool display=false)
        {
            var r=Rect(parent,name,x,y,w,h); var t=r.gameObject.AddComponent<Text>(); t.text=value; t.font=display?displayFont:uiFont;
            // Display font is reserved for Latin headings and numerals; Korean needs the UI font.
            if(display && value.Any(ch=>ch>=0xAC00 && ch<=0xD7AF)) t.font=uiFont;
            t.fontSize=size; t.color=c; t.alignment=align; t.raycastTarget=false; t.horizontalOverflow=HorizontalWrapMode.Wrap;
            t.verticalOverflow=VerticalWrapMode.Truncate; t.supportRichText=false; t.lineSpacing=1.12f; return t;
        }
        private RectTransform Button(Transform parent,string name,float x,float y,float w,float h,string value,Action action,Color back,Color fore,int size=16,bool enabled=true)
        {
            var r=Box(parent,name,x,y,w,h,back); r.GetComponent<Image>().raycastTarget=true;
            var b=r.gameObject.AddComponent<Button>(); b.targetGraphic=r.GetComponent<Image>(); b.interactable=enabled;
            var colors=b.colors; colors.normalColor=Color.white; colors.highlightedColor=new Color(1.12f,1.12f,1.12f); colors.pressedColor=new Color(.8f,.85f,.82f); colors.disabledColor=new Color(.5f,.5f,.5f,.7f); b.colors=colors;
            b.onClick.AddListener(()=>{if(!IsPresenting||name=="Skip FX")action();}); var nav=b.navigation; nav.mode=Navigation.Mode.None; b.navigation=nav;
            Text(r,"Label",4,2,w-8,h-4,value,size,enabled?fore:Hex("BAC5C0"),TextAnchor.MiddleCenter); return r;
        }
        private Sprite MakeRounded()
        {
            const int size=48,radius=12; var tex=new Texture2D(size,size,TextureFormat.RGBA32,false);
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                float dx=Mathf.Max(radius-x-.5f,x+.5f-(size-radius)),dy=Mathf.Max(radius-y-.5f,y+.5f-(size-radius));
                float d=Mathf.Sqrt(Mathf.Max(0,dx)*Mathf.Max(0,dx)+Mathf.Max(0,dy)*Mathf.Max(0,dy));
                tex.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(radius-d+.5f)));
            }
            tex.Apply(); return Sprite.Create(tex,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(radius,radius,radius,radius));
        }
        private AudioClip Tone(string title,float hz,float seconds)
        {
            int rate=22050,len=(int)(rate*seconds); var data=new float[len];
            for(int i=0;i<len;i++) { float t=(float)i/rate,fade=1f-(float)i/len; data[i]=Mathf.Sin(t*hz*2*Mathf.PI)*fade*fade*.19f; }
            var clip=AudioClip.Create(title,len,1,rate,false); clip.SetData(data,0); return clip;
        }
        private void OnDestroy() { CancelPresentation(); foreach(var sound in fxSounds)if(sound!=null)Destroy(sound); if(Instance==this)Instance=null; }
    }

    public class CardArrival : MonoBehaviour
    {
        private RectTransform rect; private Vector2 dest; private float started,delay;
        public void Setup(RectTransform r,float d) { rect=r;dest=r.anchoredPosition;started=Time.unscaledTime;delay=d;r.anchoredPosition=dest+new Vector2(0,-26); }
        private void Update()
        {
            float t=Mathf.Clamp01((Time.unscaledTime-started-delay)/.25f); rect.anchoredPosition=Vector2.Lerp(dest+new Vector2(0,-26),dest,1-Mathf.Pow(1-t,3));
            if(t>=1)Destroy(this);
        }
    }
}
