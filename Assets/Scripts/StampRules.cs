using System;
using System.Collections.Generic;
using System.Linq;

namespace NumberTable
{
    public sealed class StampOffer
    {
        public readonly int shape, keyCell;
        public bool IsKey { get { return keyCell>=0; } }
        public StampOffer(int shape,int keyCell=-1){this.shape=shape;this.keyCell=keyCell;}
    }
    public partial class TableRun
    {
        // Fixed orientations. Coordinates are never rotated or mirrored by placement.
        private static readonly int[][] stampShapes={
            new[]{0,0, 1,0, 2,0, 3,0}, // I
            new[]{0,0, 1,0, 0,1, 1,1}, // O
            new[]{0,0, 1,0, 2,0, 1,1}, // T
            new[]{0,0, 0,1, 0,2, 1,2}, // L
            new[]{1,0, 1,1, 0,2, 1,2}, // J
            new[]{1,0, 2,0, 0,1, 1,1}, // S
            new[]{0,0, 1,0, 1,1, 2,1}  // Z
        };
        public static string StampName(int shape){return new[]{"I","O","T","L","J","S","Z"}[shape];}
        public static int StampX(int shape,int cell){return stampShapes[shape][cell*2];}
        public static int StampY(int shape,int cell){return stampShapes[shape][cell*2+1];}
        public readonly List<StampOffer> stamps=new List<StampOffer>();
        private Random stampRng;
        public int[] StampCells(StampOffer offer,int anchor)
        {
            if(offer==null||offer.shape<0||offer.shape>=stampShapes.Length||anchor<2||anchor>33)return new int[0];
            var cells=new int[4];int x=(anchor-2)%5,y=(anchor-2)/5;
            for(int i=0;i<4;i++)
            {
                int xx=x+StampX(offer.shape,i),yy=y+StampY(offer.shape,i),n=2+yy*5+xx;
                if(xx>=5||n>33)return new int[0]; // includes the missing cells of the last row
                cells[i]=n;
            }
            return cells;
        }
        public bool CanStamp(StampOffer offer,int anchor)
        {
            var cells=StampCells(offer,anchor);if(cells.Length!=4)return false;
            if(offer.IsKey)return offer.keyCell<4&&!unlocked[cells[offer.keyCell]];
            return cells.Any(n=>unlocked[n]&&levels[n]<rules.levelMultipliers.Length);
        }
        private bool HasPlacement(StampOffer offer){for(int n=2;n<=33;n++)if(CanStamp(offer,n))return true;return false;}
        public int FirstStampAnchor(int index)
        {
            if(index<0||index>=stamps.Count)return -1;
            for(int n=2;n<=33;n++)if(CanStamp(stamps[index],n))return n;
            return -1;
        }
        private void BeginStamps()
        {
            stamps.Clear();var growth=new List<StampOffer>();var keys=new List<StampOffer>();
            for(int shape=0;shape<stampShapes.Length;shape++)
            {
                var offer=new StampOffer(shape);if(HasPlacement(offer))growth.Add(offer);
                for(int key=0;key<4;key++){offer=new StampOffer(shape,key);if(HasPlacement(offer))keys.Add(offer);}
            }
            if(keys.Count==0&&growth.Count==0)return; // fully upgraded board: go straight to dealer
            if(keys.Count>0)stamps.Add(keys[stampRng.Next(keys.Count)]);
            while(stamps.Count<3)
            {
                var pool=growth.Count>0?growth:keys;
                var offer=pool[stampRng.Next(pool.Count)];stamps.Add(offer);
                if(pool.Count>1)pool.Remove(offer);
            }
            // The guaranteed key is not tied to a fixed candidate slot.
            for(int i=stamps.Count-1;i>0;i--){int j=stampRng.Next(i+1);var swap=stamps[i];stamps[i]=stamps[j];stamps[j]=swap;}
            phase=RunPhase.Stamp;
            notice="블록 하나를 골라 숫자판에 찍으세요. 모양과 열쇠 위치는 고정입니다.";
        }
        public bool ApplyStamp(int index,int anchor)
        {
            if(phase!=RunPhase.Stamp||index<0||index>=stamps.Count||!CanStamp(stamps[index],anchor))return false;
            var offer=stamps[index];var cells=StampCells(offer,anchor);
            for(int i=0;i<4;i++)
            {
                int n=cells[i];
                if(i==offer.keyCell)unlocked[n]=true; // key unlocks only; no extra level on that cell
                else if(unlocked[n]&&levels[n]<rules.levelMultipliers.Length)levels[n]++;
            }
            selected=offer.IsKey?cells[offer.keyCell]:cells.First(n=>unlocked[n]);
            stamps.Clear();phase=RunPhase.Workshop;notice="숫자판 강화 완료 · 이제 카드를 정비하세요.";return true;
        }
        public void SkipStamp()
        {
            if(phase!=RunPhase.Stamp)return;
            stamps.Clear();phase=RunPhase.Workshop;notice="블록을 넘겼어요 · 카드를 정비하세요.";
        }
    }
}
