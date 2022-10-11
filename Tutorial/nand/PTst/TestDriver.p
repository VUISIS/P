machine TestAlphaSingleRW {
    var testRT: TestRoundTrip;

    start state Init {
        entry {
            var nand: Nand;
            var alpha: Alpha;

            nand = new Nand();
            alpha = new Alpha(nand);
            testRT = new TestRoundTrip(alpha);
            send testRT, eRegisterClient, this;
            goto RunTest;
        }
    }

    state RunTest {
        entry {
            var testRW: tTestReadWrite;
            testRW = (blockAddress=0, pageAddress=0, byteAddress=0, len=100);
            send testRT, eTestReadWrite, testRW;
        }
        on eTestReadWriteResp do (resp: tTestReadWriteResp) {
        }
    }
}
