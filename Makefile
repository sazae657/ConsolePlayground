SLN = ConsolePlayground.slnx
DN = dotnet

all: resx
	$(DN) build -target:ConsolePlayground -verbosity:minimal -p:Platform=x64

clean:
	$(DN) clean -verbosity:minimal

restore:
	$(DN) restore

publish: all
	$(DN) publish ConsolePlayground\ConsolePlayground.csproj -r win-x64 -p:PublishProfile=FolderProfile -c Release
