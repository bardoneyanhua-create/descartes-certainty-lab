using System.Windows;

namespace Descartes.CertaintyLab;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Window window;
        if (e.Args.Any(argument =>
                string.Equals(
                    argument,
                    "--web-reader-compatibility",
                    StringComparison.OrdinalIgnoreCase)))
        {
            window = new WebReaderCompatibilityWindow();
        }
        else if (e.Args.Any(argument =>
                string.Equals(
                    argument,
                    "--knowledge-library",
                    StringComparison.OrdinalIgnoreCase)))
        {
            window = new KnowledgeLibraryWindow();
        }
        else if (e.Args.FirstOrDefault(argument =>
                     argument.StartsWith(
                         "--learning-route=",
                         StringComparison.OrdinalIgnoreCase)) is string routeArgument)
        {
            window = new LearningRouteWindow(
                routeArgument["--learning-route=".Length..]);
        }
        else if (e.Args.Any(argument =>
                     string.Equals(
                         argument,
                         "--kant-learning-route",
                         StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("kant-foundations");
        }
        else if (e.Args.Any(argument =>
                     string.Equals(
                         argument,
                         "--epicurus-learning-route",
                         StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("epicurus-nature-pleasure-and-common-life");
        }
        else if (e.Args.Any(argument =>
                     string.Equals(
                         argument,
                         "--mill-learning-route",
                         StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("john-stuart-mill-longform");
        }
        else if (e.Args.Any(argument => string.Equals(
                     argument, "--mencius-learning-route",
                     StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("mencius-longform");
        }
        else if (e.Args.Any(argument => string.Equals(
                     argument, "--kierkegaard-learning-route",
                     StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("kierkegaard-longform");
        }
        else if (e.Args.Any(argument => string.Equals(
                     argument, "--husserl-learning-route",
                     StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("husserl-phenomenology");
        }
        else if (e.Args.Any(argument => string.Equals(
                     argument, "--xunzi-learning-route",
                     StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("xunzi-longform");
        }
        else if (e.Args.Any(argument => string.Equals(
                     argument, "--schopenhauer-learning-route",
                     StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("schopenhauer-will-representation-and-release");
        }
        else if (e.Args.Any(argument => string.Equals(
                     argument, "--sartre-learning-route",
                     StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("sartre-longform-23");
        }
        else if (e.Args.Any(argument => string.Equals(
                     argument, "--socrates-learning-route",
                     StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("socrates-many-voices");
        }
        else if (e.Args.Any(argument => string.Equals(
                     argument, "--laozi-learning-route",
                     StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("laozi-text-layers-action-and-boundaries");
        }
        else if (e.Args.Any(argument => string.Equals(
                     argument, "--mozi-learning-route",
                     StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("mozi-calibrating-standards");
        }
        else if (e.Args.Any(argument => string.Equals(
                     argument, "--francis-bacon-learning-route",
                     StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("francis-bacon-longform");
        }
        else if (e.Args.Any(argument => string.Equals(
                     argument, "--berkeley-learning-route",
                     StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("george-berkeley-longform");
        }
        else if (e.Args.Any(argument => string.Equals(
                     argument, "--heidegger-learning-route",
                     StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("heidegger-longform");
        }
        else if (e.Args.Any(argument => string.Equals(
                     argument, "--nagarjuna-learning-route",
                     StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("nagarjuna-middle-way");
        }
        else if (e.Args.Any(argument => string.Equals(
                     argument, "--plotinus-learning-route",
                     StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("plotinus-longform");
        }
        else if (e.Args.Any(argument => string.Equals(
                     argument, "--zhu-xi-learning-route",
                     StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("zhu-xi-li-xin-classics-order");
        }
        else if (e.Args.Any(argument => string.Equals(
                     argument, "--pascal-learning-route",
                     StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("pascal-longform");
        }
        else if (e.Args.Any(argument => string.Equals(
                     argument, "--dewey-learning-route",
                     StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("dewey-longform");
        }
        else if (e.Args.Any(argument => string.Equals(
                     argument, "--foucault-learning-route",
                     StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("foucault-histories-of-the-present");
        }
        else if (e.Args.Any(argument => string.Equals(
                     argument, "--buddha-learning-route",
                     StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("buddha-early-buddhism-longform");
        }
        else if (e.Args.Any(argument => string.Equals(
                     argument, "--epictetus-learning-route",
                     StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("epictetus-judgment-freedom-roles");
        }
        else if (e.Args.Any(argument => string.Equals(
                     argument, "--hanfei-learning-route",
                     StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("hanfei-standards-power-statecraft");
        }
        else if (e.Args.Any(argument =>
                     string.Equals(
                         argument,
                         "--descartes-learning-route",
                         StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("descartes-foundations");
        }
        else if (e.Args.Any(argument =>
                     string.Equals(
                         argument,
                         "--arendt-learning-route",
                         StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("arendt-foundations");
        }
        else if (e.Args.Any(argument =>
                     string.Equals(
                         argument,
                         "--hume-learning-route",
                         StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("hume-foundations");
        }
        else if (e.Args.Any(argument =>
                     string.Equals(
                         argument,
                         "--plato-learning-route",
                         StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("plato-foundations");
        }
        else if (e.Args.Any(argument =>
                     string.Equals(
                         argument,
                         "--nietzsche-learning-route",
                         StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("nietzsche-longform");
        }
        else if (e.Args.Any(argument =>
                     string.Equals(
                         argument,
                         "--aristotle-learning-route",
                         StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("aristotle-foundations");
        }
        else if (e.Args.Any(argument =>
                     string.Equals(
                         argument,
                         "--spinoza-learning-route",
                         StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("spinoza-foundations");
        }
        else if (e.Args.Any(argument =>
                     string.Equals(
                         argument,
                         "--hegel-learning-route",
                         StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("hegel-longform");
        }
        else if (e.Args.Any(argument =>
                     string.Equals(
                         argument,
                         "--zhuangzi-learning-route",
                         StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("zhuangzi-foundations");
        }
        else if (e.Args.Any(argument =>
                     string.Equals(
                         argument,
                         "--locke-learning-route",
                         StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("locke-complete-philosophy");
        }
        else if (e.Args.Any(argument =>
                     string.Equals(
                         argument,
                         "--rousseau-learning-route",
                         StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("rousseau-complete-philosophy");
        }
        else if (e.Args.Any(argument =>
                     string.Equals(
                         argument,
                         "--marx-learning-route",
                         StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("marx-foundations");
        }
        else if (e.Args.Any(argument =>
                     string.Equals(
                         argument,
                         "--confucius-learning-route",
                         StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("confucius-analects-tradition");
        }
        else if (e.Args.Any(argument =>
                     string.Equals(
                         argument,
                         "--aquinas-learning-route",
                         StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("aquinas-reason-and-order");
        }
        else if (e.Args.Any(argument =>
                     string.Equals(
                         argument,
                         "--wittgenstein-learning-route",
                         StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("wittgenstein-language-and-world");
        }
        else if (e.Args.Any(argument =>
                     string.Equals(
                         argument,
                         "--beauvoir-learning-route",
                         StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("beauvoir-freedom-situation");
        }
        else if (e.Args.Any(argument =>
                     string.Equals(
                         argument,
                         "--augustine-learning-route",
                         StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("augustine-longform");
        }
        else if (e.Args.Any(argument =>
                     string.Equals(
                         argument,
                         "--leibniz-learning-route",
                         StringComparison.OrdinalIgnoreCase)))
        {
            window = new LearningRouteWindow("leibniz-reasons-and-worlds");
        }
        else if (e.Args.Any(argument =>
                     string.Equals(
                         argument,
                         "--arendt",
                         StringComparison.OrdinalIgnoreCase)))
        {
            window = new ArendtWindow();
        }
        else
        {
            window = new ExperienceCatalogWindow();
        }
        MainWindow = window;
        window.Show();
    }
}
