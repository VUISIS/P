
type tTestBuffer = (buffer: seq[int], len: int);

type tTestReadWrite = (blockAddress: int, pageAddress: int, byteAddress: int, len: int);
type tTestReadWriteResp = int;

event eTestingWrite: tTestBuffer;
event eTestingRead: tTestBuffer;

event eTestReadWrite : tTestReadWrite;
event eTestReadWriteResp : tTestReadWriteResp;

machine TestRoundTrip {
    var testSendBuffer: seq[int];
    var testSendLen: int;
    var tester: NandTester;
    var client: machine;
    var blockAddress: int;
    var pageAddress: int;
    var byteAddress: int;

    start state Init {
        entry (init: tNandTesterInit) {
            tester = new NandTester(init);

        }

        on eRegisterClient do (clientRef: tRegisterClient) {
            client = clientRef;
            send tester, eRegisterClient, this;
        }

        on eRegisterClientResp do {
            send client, eRegisterClientResp;
            goto awaitRequest;
        }
    }

    state awaitRequest {
        on eTestReadWrite do (req: tTestReadWrite) {
            var sendBuff: seq[int];
            var writeTest: tWriteTestRequest;
            blockAddress = req.blockAddress;
            pageAddress = req.pageAddress;
            byteAddress = req.byteAddress;
            testSendLen = 0;
            while (testSendLen < req.len) {
                sendBuff += (testSendLen, choose(256));
                testSendLen = testSendLen + 1;
            }
            testSendBuffer = sendBuff;
            writeTest = (blockAddress=req.blockAddress, pageAddress=req.pageAddress, byteAddress=req.byteAddress, buffer = sendBuff, len=req.len);
            send tester, eWriteTestRequest, writeTest;
            goto awaitWriteResponse;
        }
    }

    state awaitWriteResponse {
        on eWriteTestResponse do (resp: tWriteTestResponse) {
            var readReq : tReadTestRequest;
            if (resp < 0) {
                send client, eTestReadWriteResp, -1;
                goto awaitRequest;
            }
            readReq = (blockAddress=blockAddress, pageAddress=pageAddress, byteAddress=byteAddress, len=testSendLen);
            send tester, eReadTestRequest, readReq;
            goto awaitReadResponse;
        }
    }

    state awaitReadResponse {
        on eReadTestResponse do (resp: tReadTestResponse) {
            var i: int;
            if (resp.len < 0) {
                send client, eTestReadWriteResp, -1;
                goto awaitRequest;
            }

            if (resp.len != testSendLen) {
                send client, eTestReadWriteResp, -2;
                goto awaitRequest;
            }

            i = 0;
            while (i < resp.len) {
                if (resp.buffer[i] != testSendBuffer[i]) {
                    send client, eTestReadWriteResp, -3;
                    goto awaitRequest;
                }
                i = i + 1;
            }

            send client, eTestReadWriteResp, testSendLen;
            goto awaitRequest;
        }
    }
}
