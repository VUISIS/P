machine TestAlpha0SingleRW {
    start state Init {
        entry {
            var alpha: Alpha0;
            var testRW: TestAlphaSingleRW;
            var nand: Nand;
            nand = new Nand();
            alpha = new Alpha0(nand);
            testRW = new TestAlphaSingleRW(alpha);
        }
    }
}

machine TestAlpha4SingleRW {
    start state Init {
        entry {
            var alpha: Alpha4;
            var testRW: TestAlphaSingleRW;
            var nand: Nand;
            nand = new Nand();
            alpha = new Alpha4(nand);
            testRW = new TestAlphaSingleRW(alpha);
        }
    }
}

machine TestAlpha5SingleRW {
    start state Init {
        entry {
            var alpha: Alpha5;
            var testRW: TestAlphaSingleRW;
            var nand: Nand;
            nand = new Nand();
            alpha = new Alpha5(nand);
            testRW = new TestAlphaSingleRW(alpha);
        }
    }
}

machine TestAlpha6SingleRW {
    start state Init {
        entry {
            var alpha: Alpha6;
            var testRW: TestAlphaSingleRW;
            var nand: Nand;
            nand = new Nand();
            alpha = new Alpha6(nand);
            testRW = new TestAlphaSingleRW(alpha);
        }
    }
}

machine TestAlphaSingleRW {
    var testRT: TestRoundTrip;

    start state Init {
        entry (driver: machine) {
            var alpha: machine;
            var tester: machine;

            alpha = driver;
            tester = new NandTester(alpha);
            testRT = new TestRoundTrip(tester);

            send testRT, eRegisterClient, this;
        }
        on eRegisterClientResp do {
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


machine TestFoxtrot0SingleRW {
    start state Init {
        entry {
            var foxtrot: Foxtrot0;
            var testRW: TestFoxtrotSingleRW;
            var nand: Nand;
            nand = new Nand();
            foxtrot = new Foxtrot0(nand);
            testRW = new TestFoxtrotSingleRW(foxtrot);
        }
    }
}

machine TestFoxtrot1SingleRW {
    start state Init {
        entry {
            var foxtrot: Foxtrot1;
            var testRW: TestFoxtrotSingleRW;
            var nand: Nand;
            nand = new Nand();
            foxtrot = new Foxtrot1(nand);
            testRW = new TestFoxtrotSingleRW(foxtrot);
        }
    }
}

machine TestFoxtrot2SingleRW {
    start state Init {
        entry {
            var foxtrot: Foxtrot2;
            var testRW: TestFoxtrotSingleRW;
            var nand: Nand;
            nand = new Nand();
            foxtrot = new Foxtrot2(nand);
            testRW = new TestFoxtrotSingleRW(foxtrot);
        }
    }
}

machine TestFoxtrotSingleRW {
    var testRT: TestRoundTrip;

    start state Init {
        entry (driver: machine) {
            var foxtrot: machine;
            var tester: machine;

            foxtrot = driver;
            tester = new NandCommandTester(foxtrot);
            testRT = new TestRoundTrip(tester);

            send testRT, eRegisterClient, this;
        }
        on eRegisterClientResp do {
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
