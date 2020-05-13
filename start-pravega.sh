# this works on MacOS
#export HOST_IP=`ipconfig getifaddr en0`

if [ -z "$HOST_IP" ]; then
  echo "Please set HOST_IP env var to the IP of your machine"
  exit
fi

export PRAVEGA_CONTROLLER=tcp://$HOST_IP:9090

# start the pravega and pravega-grpc-gateway docker containers
# in the background
docker-compose up -d

