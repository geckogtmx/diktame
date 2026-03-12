import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import rehypeRaw from 'rehype-raw';

interface MarkdownRendererProps {
  content: string;
}

export function MarkdownRenderer({ content }: MarkdownRendererProps) {
  // Pre-process GitHub alerts to render them with custom styling
  // Matches: > [!TIP] or > [!NOTE] followed by any text until a double newline
  const processedContent = content.replace(
    /> \[!([A-Z]+)\]\n> (.*)/g,
    (_, type, message) => {
      return `<div data-alert="${type.toLowerCase()}"><strong>${type}</strong><br/>${message}</div>\n\n`;
    }
  );

  return (
    <article className="prose prose-invert prose-blue max-w-none 
      prose-headings:font-bold prose-headings:tracking-tight
      prose-a:text-blue-400 prose-a:no-underline hover:prose-a:underline
      prose-code:bg-gray-800 prose-code:text-blue-300 prose-code:px-1 prose-code:py-0.5 prose-code:rounded prose-code:before:content-none prose-code:after:content-none
      prose-pre:bg-gray-900 prose-pre:border prose-pre:border-white/10
      prose-img:rounded-lg prose-img:border prose-img:border-white/10
      prose-blockquote:border-l-blue-500 prose-blockquote:bg-blue-500/10 prose-blockquote:py-1 prose-blockquote:px-4 prose-blockquote:rounded-r
    ">
      <ReactMarkdown 
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[rehypeRaw]}
        components={{
          div(props: any) {
            // Handle our custom parsed alerts
            if (props['data-alert']) {
              const type = props['data-alert'];
              let bg = 'bg-gray-800/50';
              let border = 'border-gray-500';
              let text = 'text-gray-200';
              
              if (type === 'tip') { bg = 'bg-emerald-900/30'; border = 'border-emerald-500'; text = 'text-emerald-200'; }
              if (type === 'note') { bg = 'bg-blue-900/30'; border = 'border-blue-500'; text = 'text-blue-200'; }
              if (type === 'warning') { bg = 'bg-amber-900/30'; border = 'border-amber-500'; text = 'text-amber-200'; }
              if (type === 'important') { bg = 'bg-purple-900/30'; border = 'border-purple-500'; text = 'text-purple-200'; }
              if (type === 'caution') { bg = 'bg-red-900/30'; border = 'border-red-500'; text = 'text-red-200'; }

              return (
                <div className={`my-6 p-4 rounded-lg border-l-4 ${bg} ${border} ${text}`}>
                  {props.children}
                </div>
              );
            }
            return <div {...props} />;
          }
        }}
      >
        {processedContent}
      </ReactMarkdown>
    </article>
  );
}
