FROM dcreg.service.consul/dev/development-dotnet-core-sdk-common:3.1

# build scripts
COPY ./fake.sh /lib/
COPY ./build.fsx /lib/
COPY ./paket.dependencies /lib/
COPY ./paket.references /lib/
COPY ./paket.lock /lib/

# sources
COPY ./JsonApi.fsproj /lib/
COPY ./src /lib/src

# others
COPY ./.git /lib/.git
COPY ./CHANGELOG.md /lib/

WORKDIR /lib

RUN \
    ./fake.sh build target Build no-clean

CMD ["./fake.sh", "build", "target", "Tests", "no-clean"]
