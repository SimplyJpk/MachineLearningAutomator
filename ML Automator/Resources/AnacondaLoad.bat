ECHO Launching Anaconda
REM Activate our Conda Environment
CALL conda activate L:/Anaconda/envs/ml-agents
REM Launch our Learning
CALL mlagents-learn %*