FROM debian:13-slim
RUN apt-get update && apt-get install -y --no-install-recommends mono-devel libgdiplus xvfb xauth fonts-dejavu-core ca-certificates && rm -rf /var/lib/apt/lists/*
WORKDIR /src
CMD ["bash", "linux/build.sh"]
