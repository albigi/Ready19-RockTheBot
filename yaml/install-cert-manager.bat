@ECHO OFF
ECHO Installing the cert-manager in namespace cert-manager
kubectl create namespace cert-manager
kubectl label namespace cert-manager certmanager.k8s.io/disable-validation=true
kubectl apply -f https://raw.githubusercontent.com/jetstack/cert-manager/release-0.6/deploy/manifests/00-crds.yaml
kubectl apply -f https://raw.githubusercontent.com/jetstack/cert-manager/release-0.6/deploy/manifests/cert-manager.yaml --validate=false

ECHO Creating the production cluster issuer (LetsEncrypt)
kubectl apply -f create-cluster-issuer.yaml

ECHO All done!
PAUSE